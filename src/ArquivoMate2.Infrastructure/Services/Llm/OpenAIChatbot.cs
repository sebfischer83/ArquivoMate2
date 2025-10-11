using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public class OpenAIChatBot : IChatBot
    {
        private readonly ChatClient _client;

        private const string LoadChunkToolName = "load_document_chunk";
        private const string QueryDocumentsToolName = "query_documents";

        private static readonly JsonSerializerOptions s_toolSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public OpenAIChatBot(ChatClient client)
        {
            _client = client;
        }

        public string ModelName => _client.Model;

        public async Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, CancellationToken cancellationToken)
        {
            var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are an assistant that analyzes the document and ALWAYS returns JSON according to the defined schema. Respond in German. Suggest maximum of 5 keywords. The summary should not exceed 500 characters. Let fields empty if you can't fill them. The title should be a very short description of the content."),
                    new UserChatMessage($"Document text:\n{content}")
                };

            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "analyze_document",
                    jsonSchema: BinaryData.FromString(OpenAIHelper.SchemaJson),
                    jsonSchemaIsStrict: true)
            };

            cancellationToken.ThrowIfCancellationRequested();
            ChatCompletion response = await _client.CompleteChatAsync(messages, options);

            string jsonText = response.Content[0].Text;

            return JsonSerializer.Deserialize<DocumentAnalysisResult>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }

        public async Task<DocumentAnswerResult> AnswerQuestion(DocumentQuestionContext context, string question, IDocumentQuestionTooling tooling, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                throw new ArgumentException("Question must not be empty", nameof(question));
            }

            if (tooling is null)
            {
                throw new ArgumentNullException(nameof(tooling));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var chunkMap = SplitIntoChunks(context.Content);
            var metadata = BuildMetadataSection(context, chunkMap.Values);

            const string systemPrompt = "You are a helpful assistant answering questions about the user's documents. Use the available tools to gather facts before replying. Always call load_document_chunk before citing text from the active document. When the user asks about other documents, statistics, or filters, you MUST call query_documents to retrieve real data. Never invent document identifiers. Return the final response as compact JSON with fields: 'answer' (string), 'citations' (array of objects with 'chunk_id' and optional 'quote'), optional 'documents' (array of objects with 'id', 'title', 'summary', optional 'date', 'file_size_bytes', 'score'), and optional 'document_count' (number).";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(metadata + "\nQuestion: " + question.Trim())
            };

            var options = new ChatCompletionOptions
            {
                ToolChoice = ChatToolChoice.CreateAutoChoice()
            };
            options.Tools.Add(CreateChunkTool());
            options.Tools.Add(CreateDocumentQueryTool());

            ChatCompletion response = await _client.CompleteChatAsync(messages, options, cancellationToken);

            var iterations = 0;
            const int maxIterations = 8;

            while (response.ToolCalls.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                foreach (var toolCall in response.ToolCalls.OfType<ChatFunctionToolCall>())
                {
                    if (string.Equals(toolCall.Name, LoadChunkToolName, StringComparison.Ordinal))
                    {
                        var chunkId = TryGetChunkId(toolCall);
                        var chunkContent = chunkId != null && chunkMap.TryGetValue(chunkId, out var chunk)
                            ? chunk.Content
                            : string.Empty;

                        var toolPayload = JsonSerializer.Serialize(new
                        {
                            chunk_id = chunkId,
                            content = chunkContent
                        }, s_toolSerializerOptions);

                        messages.Add(new ToolChatMessage(toolCall.Id, toolPayload));
                    }
                    else if (string.Equals(toolCall.Name, QueryDocumentsToolName, StringComparison.Ordinal))
                    {
                        var query = TryParseDocumentQuery(toolCall);
                        try
                        {
                            var result = query != null
                                ? await tooling.QueryDocumentsAsync(query, cancellationToken)
                                : new DocumentQueryResult();

                            var toolPayload = SerializeDocumentQueryResult(result);
                            messages.Add(new ToolChatMessage(toolCall.Id, toolPayload));
                        }
                        catch (Exception ex)
                        {
                            var errorPayload = JsonSerializer.Serialize(new
                            {
                                error = "query_failed",
                                message = ex.Message
                            }, s_toolSerializerOptions);
                            messages.Add(new ToolChatMessage(toolCall.Id, errorPayload));
                        }
                    }
                }

                response = await _client.CompleteChatAsync(messages, options, cancellationToken);
            }

            var answerText = response.Content.FirstOrDefault()?.Text?.Trim() ?? string.Empty;

            return ParseFinalAnswer(answerText, chunkMap, ModelName);
        }

        private static string TryGetChunkId(ChatFunctionToolCall toolCall)
        {
            if (toolCall?.Arguments is null)
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(toolCall.Arguments.ToString());
                if (document.RootElement.TryGetProperty("chunk_id", out var chunkIdProp))
                {
                    return chunkIdProp.GetString();
                }
            }
            catch
            {
                // Ignore malformed payloads and fall back to null.
            }

            return null;
        }

        private static DocumentAnswerResult ParseFinalAnswer(string answerPayload, IReadOnlyDictionary<string, DocumentChunk> chunkMap, string modelName)
        {
            if (string.IsNullOrWhiteSpace(answerPayload))
            {
                return new DocumentAnswerResult
                {
                    Answer = string.Empty,
                    Model = modelName,
                    Citations = Array.Empty<DocumentAnswerCitation>(),
                    Documents = Array.Empty<DocumentAnswerReference>(),
                    DocumentCount = null
                };
            }

            try
            {
                using var document = JsonDocument.Parse(answerPayload);
                var root = document.RootElement;
                var answer = root.TryGetProperty("answer", out var answerProp)
                    ? answerProp.GetString() ?? string.Empty
                    : answerPayload;

                var citations = new List<DocumentAnswerCitation>();
                var documents = new List<DocumentAnswerReference>();
                long? documentCount = null;
                if (root.TryGetProperty("citations", out var citationsProp) && citationsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var citation in citationsProp.EnumerateArray())
                    {
                        var chunkId = citation.TryGetProperty("chunk_id", out var chunkIdProp)
                            ? chunkIdProp.GetString()
                            : null;
                        var quote = citation.TryGetProperty("quote", out var quoteProp)
                            ? quoteProp.GetString()
                            : null;

                        var chunkKey = chunkId ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(chunkKey) && chunkMap.TryGetValue(chunkKey, out var chunk))
                        {
                            citations.Add(new DocumentAnswerCitation
                            {
                                Source = chunkKey,
                                Snippet = string.IsNullOrWhiteSpace(quote)
                                    ? TrimSnippet(chunk.Content)
                                    : quote ?? string.Empty
                            });
                        }
                        else if (!string.IsNullOrWhiteSpace(quote))
                        {
                            citations.Add(new DocumentAnswerCitation
                            {
                                Source = chunkId,
                                Snippet = quote ?? string.Empty
                            });
                        }
                    }
                }

                if (root.TryGetProperty("documents", out var documentsProp) && documentsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var documentEntry in documentsProp.EnumerateArray())
                    {
                        if (documentEntry.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        if (!documentEntry.TryGetProperty("id", out var idProp))
                        {
                            continue;
                        }

                        var idString = idProp.GetString();
                        if (!Guid.TryParse(idString, out var documentId))
                        {
                            continue;
                        }

                        double? score = null;
                        if (documentEntry.TryGetProperty("score", out var scoreProp))
                        {
                            if (scoreProp.ValueKind == JsonValueKind.Number)
                            {
                                score = scoreProp.GetDouble();
                            }
                            else if (scoreProp.ValueKind == JsonValueKind.String && double.TryParse(scoreProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var scoreValue))
                            {
                                score = scoreValue;
                            }
                        }

                        long? fileSizeBytes = null;
                        if (documentEntry.TryGetProperty("file_size_bytes", out var sizeProp))
                        {
                            if (sizeProp.ValueKind == JsonValueKind.Number && sizeProp.TryGetInt64(out var sizeValue))
                            {
                                fileSizeBytes = sizeValue;
                            }
                            else if (sizeProp.ValueKind == JsonValueKind.String && long.TryParse(sizeProp.GetString(), out var parsedSize))
                            {
                                fileSizeBytes = parsedSize;
                            }
                        }

                        DateTime? date = null;
                        if (documentEntry.TryGetProperty("date", out var dateProp))
                        {
                            if (dateProp.ValueKind == JsonValueKind.String && DateTime.TryParse(dateProp.GetString(), null, DateTimeStyles.RoundtripKind, out var parsedDate))
                            {
                                date = parsedDate;
                            }
                        }

                        documents.Add(new DocumentAnswerReference
                        {
                            DocumentId = documentId,
                            Title = documentEntry.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null,
                            Summary = documentEntry.TryGetProperty("summary", out var summaryProp) ? summaryProp.GetString() : null,
                            Date = date,
                            Score = score,
                            FileSizeBytes = fileSizeBytes
                        });
                    }
                }

                if (root.TryGetProperty("document_count", out var countProp))
                {
                    if (countProp.ValueKind == JsonValueKind.Number && countProp.TryGetInt64(out var countValue))
                    {
                        documentCount = countValue;
                    }
                    else if (countProp.ValueKind == JsonValueKind.String && long.TryParse(countProp.GetString(), out var parsedCount))
                    {
                        documentCount = parsedCount;
                    }
                }

                return new DocumentAnswerResult
                {
                    Answer = answer,
                    Model = modelName,
                    Citations = citations,
                    Documents = documents,
                    DocumentCount = documentCount
                };
            }
            catch (JsonException)
            {
                // Fallback to plain text if parsing fails.
                return new DocumentAnswerResult
                {
                    Answer = answerPayload,
                    Model = modelName,
                    Citations = Array.Empty<DocumentAnswerCitation>(),
                    Documents = Array.Empty<DocumentAnswerReference>(),
                    DocumentCount = null
                };
            }
        }

        private static string TrimSnippet(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            const int maxLength = 400;
            return content.Length <= maxLength
                ? content
                : content.Substring(0, maxLength) + "…";
        }

        private static ChatTool CreateChunkTool()
        {
            return ChatTool.CreateFunctionTool(
                functionName: LoadChunkToolName,
                functionDescription: "Loads the full text for one of the document chunks listed in the metadata.",
                parameters: BinaryData.FromString(@"{
  \"type\": \"object\",
  \"properties\": {
    \"chunk_id\": {
      \"type\": \"string\",
      \"description\": \"Identifier of the chunk to load, e.g. chunk_1\"
    }
  },
  \"required\": [\"chunk_id\"]
}"));
        }

        private static ChatTool CreateDocumentQueryTool()
        {
            return ChatTool.CreateFunctionTool(
                functionName: QueryDocumentsToolName,
                functionDescription: "Searches or filters the user's document catalogue and returns identifiers, metadata, or counts.",
                parameters: BinaryData.FromString(@"{
  \"type\": \"object\",
  \"properties\": {
    \"search\": {
      \"type\": \"string\",
      \"description\": \"Optional full-text search query or keywords. Leave empty when only filters are used.\"
    },
    \"limit\": {
      \"type\": \"integer\",
      \"minimum\": 1,
      \"maximum\": 20,
      \"description\": \"Maximum number of document hits to return.\"
    },
    \"projection\": {
      \"type\": \"string\",
      \"enum\": [\"documents\", \"count\", \"both\"],
      \"description\": \"Whether the model needs document hits, an aggregate count, or both.\"
    },
    \"filters\": {
      \"type\": \"object\",
      \"properties\": {
        \"document_ids\": {
          \"type\": \"array\",
          \"items\": {
            \"type\": \"string\",
            \"description\": \"Document GUID to retrieve details for.\"
          }
        },
        \"year\": {
          \"type\": \"integer\",
          \"description\": \"Calendar year based on the document's logical date.\"
        },
        \"min_file_size_mb\": {
          \"type\": \"number\",
          \"description\": \"Minimum original file size in megabytes.\"
        },
        \"max_file_size_mb\": {
          \"type\": \"number\",
          \"description\": \"Maximum original file size in megabytes.\"
        },
        \"type\": {
          \"type\": \"string\",
          \"description\": \"Optional document type label.\"
        }
      }
    }
  }
}"));
        }

        private static string SerializeDocumentQueryResult(DocumentQueryResult? result)
        {
            result ??= new DocumentQueryResult();

            var payload = new
            {
                documents = result.Documents.Select(d => new
                {
                    id = d.DocumentId,
                    title = d.Title,
                    summary = d.Summary,
                    date = d.Date?.ToString("o"),
                    file_size_bytes = d.FileSizeBytes,
                    score = d.Score
                }),
                total_count = result.TotalCount
            };

            return JsonSerializer.Serialize(payload, s_toolSerializerOptions);
        }

        private static DocumentQuery? TryParseDocumentQuery(ChatFunctionToolCall toolCall)
        {
            if (toolCall?.Arguments is null)
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(toolCall.Arguments.ToString());
                var root = document.RootElement;

                var limit = 10;
                if (root.TryGetProperty("limit", out var limitProp))
                {
                    limit = ParseInt(limitProp, 10);
                }

                var projection = DocumentQueryProjection.Documents;
                if (root.TryGetProperty("projection", out var projectionProp))
                {
                    var projectionValue = projectionProp.GetString();
                    if (!string.IsNullOrWhiteSpace(projectionValue))
                    {
                        projection = projectionValue.Trim().ToLowerInvariant() switch
                        {
                            "count" => DocumentQueryProjection.Count,
                            "both" => DocumentQueryProjection.Both,
                            _ => DocumentQueryProjection.Documents
                        };
                    }
                }

                var filters = new DocumentQueryFilters();
                if (root.TryGetProperty("filters", out var filtersProp) && filtersProp.ValueKind == JsonValueKind.Object)
                {
                    filters = ParseFilters(filtersProp);
                }

                var search = root.TryGetProperty("search", out var searchProp) ? searchProp.GetString() : null;

                return new DocumentQuery
                {
                    Search = string.IsNullOrWhiteSpace(search) ? null : search,
                    Filters = filters,
                    Projection = projection,
                    Limit = Math.Clamp(limit, 1, 20)
                };
            }
            catch
            {
                return null;
            }
        }

        private static DocumentQueryFilters ParseFilters(JsonElement filtersProp)
        {
            var ids = new List<Guid>();
            if (filtersProp.TryGetProperty("document_ids", out var idsProp) && idsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var idElement in idsProp.EnumerateArray())
                {
                    var idString = idElement.GetString();
                    if (!string.IsNullOrWhiteSpace(idString) && Guid.TryParse(idString, out var guid))
                    {
                        ids.Add(guid);
                    }
                }
            }

            int? year = null;
            if (filtersProp.TryGetProperty("year", out var yearProp))
            {
                year = ParseNullableInt(yearProp);
            }

            var minSize = TryParseNullableDouble(filtersProp, "min_file_size_mb");
            var maxSize = TryParseNullableDouble(filtersProp, "max_file_size_mb");
            var type = filtersProp.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

            return new DocumentQueryFilters
            {
                DocumentIds = ids,
                Year = year,
                MinFileSizeMb = minSize,
                MaxFileSizeMb = maxSize,
                Type = string.IsNullOrWhiteSpace(type) ? null : type
            };
        }

        private static int ParseInt(JsonElement element, int fallback)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static int? ParseNullableInt(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static double? TryParseNullableDouble(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var element))
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetDouble();
            }

            if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static Dictionary<string, DocumentChunk> SplitIntoChunks(string content)
        {
            var chunks = new Dictionary<string, DocumentChunk>();

            if (string.IsNullOrEmpty(content))
            {
                return chunks;
            }

            const int chunkSize = 1200;
            var index = 0;
            var position = 0;

            while (position < content.Length)
            {
                var length = Math.Min(chunkSize, content.Length - position);
                var slice = content.Substring(position, length);
                var id = $"chunk_{++index}";

                var start = position;
                var end = position + length;

                chunks[id] = new DocumentChunk(id, slice, index, start, end);
                position += length;
            }

            return chunks;
        }

        private static string BuildMetadataSection(DocumentQuestionContext context, IEnumerable<DocumentChunk> chunks)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Document metadata:");
            if (!string.IsNullOrWhiteSpace(context.Title))
            {
                builder.AppendLine($"- Title: {context.Title}");
            }
            if (!string.IsNullOrWhiteSpace(context.Summary))
            {
                builder.AppendLine($"- Summary: {context.Summary}");
            }
            if (context.Keywords?.Count > 0)
            {
                builder.AppendLine($"- Keywords: {string.Join(", ", context.Keywords)}");
            }
            if (!string.IsNullOrWhiteSpace(context.Language))
            {
                builder.AppendLine($"- Language: {context.Language}");
            }

            if (context.History?.Count > 0)
            {
                builder.AppendLine("- Recent history:");
                foreach (var entry in context.History.Take(5))
                {
                    builder.AppendLine($"  * {entry}");
                }
            }

            builder.AppendLine();
            var orderedChunks = chunks.OrderBy(c => c.Index).ToList();
            builder.AppendLine($"Available document chunks ({orderedChunks.Count} total). Use load_document_chunk to fetch their contents:");
            foreach (var chunk in orderedChunks)
            {
                builder.AppendLine($"- {chunk.Id}: characters {chunk.StartPosition + 1}-{chunk.EndPosition}");
            }

            builder.AppendLine();
            builder.AppendLine("Use query_documents to search for similar or related documents, filter by metadata, or retrieve counts when the user asks.");

            return builder.ToString();
        }

        private sealed record DocumentChunk(string Id, string Content, int Index, int StartPosition, int EndPosition);
    }
}
