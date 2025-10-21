using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Infrastructure.Configuration.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public class OpenRouterChatBot : IChatBot
    {
        private readonly HttpClient _http;
        private readonly OpenRouterSettings _settings;
        private readonly ILogger<OpenRouterChatBot> _logger;

        private const string LoadChunkToolName = "load_document_chunk";
        private const string QueryDocumentsToolName = "query_documents";

        private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public OpenRouterChatBot(HttpClient http, IOptions<OpenRouterSettings> opts, ILogger<OpenRouterChatBot> logger)
        {
            _http = http;
            _settings = opts.Value;
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            }
            // OpenRouter headers (application name + referer) improve routing & quota attribution
            if (!_http.DefaultRequestHeaders.Contains("HTTP-Referer"))
            {
                _http.DefaultRequestHeaders.Add("HTTP-Referer", "https://arquivomate2.local");
            }
            if (!_http.DefaultRequestHeaders.Contains("X-Title"))
            {
                _http.DefaultRequestHeaders.Add("X-Title", "ArquivoMate2");
            }

            _http.BaseAddress = new Uri(_settings.BaseUrl);
        }

        public string ModelName => _settings.Model;

        public async Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, IReadOnlyList<DocumentTypeOption> availableTypes, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(content)) return new DocumentAnalysisResult();

            var schemaJson = OpenAIHelper.BuildSchemaJson(availableTypes?.Select(t => t.Name) ?? Array.Empty<string>());
            var language = string.IsNullOrWhiteSpace(_settings.ServerLanguage) ? "German" : _settings.ServerLanguage;
            var typeNames = (availableTypes ?? Array.Empty<DocumentTypeOption>())
                .Select(t => t?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var typeInstruction = typeNames.Count > 0
                ? $"Select the documentType from this list and return the exact value: {string.Join(", ", typeNames)}."
                : "If you cannot determine a documentType, leave the field empty.";
            var systemPrompt = $"You are an assistant that analyzes the document and ALWAYS returns ONLY JSON matching the schema. Respond in {language}. {typeInstruction} Suggest maximum of 5 keywords. Summary <= 500 chars. Empty fields if unknown. Title must be very short.";

            // OpenRouter (OpenAI compatible) structured output via response_format json_schema
            var payload = new
            {
                model = _settings.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = content }
                },
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "analyze_document",
                        schema = JsonDocument.Parse(schemaJson).RootElement
                    }
                },
                max_tokens = 1500
            };

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, cancellationToken);
            var respText = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenRouter analyze failed: {Status} {Body}", resp.StatusCode, Truncate(respText, 300));
                return new DocumentAnalysisResult();
            }

            try
            {
                using var doc = JsonDocument.Parse(respText);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    string jsonText = ExtractMessageContent(first);
                    if (!string.IsNullOrWhiteSpace(jsonText))
                    {
                        var result = JsonSerializer.Deserialize<DocumentAnalysisResult>(jsonText, s_jsonOptions);
                        return result ?? new DocumentAnalysisResult();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed parsing analysis response");
            }
            return new DocumentAnalysisResult();
        }

        public async Task<DocumentAnswerResult> AnswerQuestion(DocumentQuestionContext context, string question, IDocumentQuestionTooling tooling, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("Question must not be empty", nameof(question));
            if (tooling == null) throw new ArgumentNullException(nameof(tooling));

            // Chunking (simple fixed size similar to OpenAIChatBot)
            var chunkMap = SplitIntoChunks(context.Content);

            // System prompt instructing tool usage & JSON output
            const string systemPrompt = "You answer user questions about the document. Use load_document_chunk before citing text. For catalogue/search queries use query_documents. Return final answer ONLY as JSON: {answer, citations:[{chunk_id, quote?}], documents:[{id,title,summary,date,file_size_bytes,score?}], document_count?}.";

            var metadata = BuildMetadata(context, chunkMap.Values.OrderBy(c => c.Index));

            var messages = new List<Dictionary<string, string>>
            {
                new() { ["role"] = "system", ["content"] = systemPrompt },
                new() { ["role"] = "user", ["content"] = metadata + "\nQuestion: " + question.Trim() }
            };

            var tools = new[] { CreateChunkTool(), CreateDocumentQueryTool() };
            var request = new
            {
                model = _settings.Model,
                messages,
                tools,
                tool_choice = "auto",
                max_tokens = 1800
            };

            var response = await SendCompletionAsync(request, cancellationToken);
            int iterations = 0;
            const int maxIterations = 6;
            while (response.ToolCalls.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                foreach (var call in response.ToolCalls)
                {
                    if (string.Equals(call.Name, LoadChunkToolName, StringComparison.Ordinal))
                    {
                        var chunkId = call.Arguments.TryGetValue("chunk_id", out var cid) ? cid : null;
                        var chunkContent = chunkId != null && chunkMap.TryGetValue(chunkId, out var c) ? c.Content : string.Empty;
                        messages.Add(new Dictionary<string, string>
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = call.Id,
                            ["content"] = JsonSerializer.Serialize(new { chunk_id = chunkId, content = chunkContent })
                        });
                    }
                    else if (string.Equals(call.Name, QueryDocumentsToolName, StringComparison.Ordinal))
                    {
                        var query = TryParseDocumentQuery(call.Arguments);
                        DocumentQueryResult result;
                        try
                        {
                            result = query != null ? await tooling.QueryDocumentsAsync(query, cancellationToken) : new DocumentQueryResult();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "query_documents tooling failed");
                            result = new DocumentQueryResult();
                        }

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
                        messages.Add(new Dictionary<string, string>
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = call.Id,
                            ["content"] = JsonSerializer.Serialize(payload)
                        });
                    }
                }

                var loopRequest = new
                {
                    model = _settings.Model,
                    messages,
                    tools,
                    tool_choice = "auto",
                    max_tokens = 1800
                };
                response = await SendCompletionAsync(loopRequest, cancellationToken);
            }

            var answerText = response.FinalContent?.Trim() ?? string.Empty;
            return ParseFinalAnswer(answerText, chunkMap, ModelName);
        }

        public Task<DocumentAnswerResult> AnswerQuestionWithPrompt(string question, string documentContent, string? structuredJsonSchema, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("Question must not be empty", nameof(question));

            // If no structured schema requested, delegate to existing AnswerQuestion flow
            if (string.IsNullOrWhiteSpace(structuredJsonSchema))
            {
                var ctx = new DocumentQuestionContext
                {
                    Content = documentContent ?? string.Empty,
                    DocumentId = Guid.Empty,
                    UserId = null,
                    Title = string.Empty,
                    Summary = null,
                    Keywords = Array.Empty<string>(),
                    Language = _settings.ServerLanguage
                };

                IDocumentQuestionTooling tooling = new NoopTooling();
                return AnswerQuestion(ctx, question, tooling, cancellationToken);
            }

            // Build payload asking for json_schema response_format
            var messages = new[]
            {
                new { role = "system", content = "Answer the user's question and RETURN ONLY JSON matching the supplied schema." },
                new { role = "user", content = documentContent + "\n\nQuestion: " + question }
            };

            var request = new
            {
                model = _settings.Model,
                messages,
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "answer_document_question",
                        schema = JsonDocument.Parse(structuredJsonSchema).RootElement
                    }
                },
                max_tokens = 1600
            };

            var completion = SendCompletionAsync(request, cancellationToken).GetAwaiter().GetResult();
            var answerText = completion.FinalContent ?? string.Empty;
            var parsed = ParseFinalAnswer(answerText, new Dictionary<string, DocumentChunk>(), ModelName);
            return Task.FromResult(new DocumentAnswerResult
            {
                Answer = parsed.Answer,
                Model = parsed.Model,
                Citations = parsed.Citations,
                Documents = parsed.Documents,
                DocumentCount = parsed.DocumentCount,
                StructuredJson = string.IsNullOrWhiteSpace(answerText) ? null : answerText
            });
        }

        private sealed class NoopTooling : IDocumentQuestionTooling
        {
            public Task<DocumentQueryResult> QueryDocumentsAsync(DocumentQuery query, CancellationToken cancellationToken)
            {
                return Task.FromResult(new DocumentQueryResult());
            }
        }

        #region Internal request helpers
        private async Task<CompletionResponse> SendCompletionAsync(object payload, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenRouter chat error {Status}: {Snippet}", resp.StatusCode, Truncate(body, 300));
                return CompletionResponse.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var choices = root.GetProperty("choices");
                if (choices.GetArrayLength() == 0) return CompletionResponse.Empty;
                var first = choices[0];
                string finalContent = ExtractMessageContent(first) ?? string.Empty;

                var toolCalls = new List<ToolCall>();
                if (first.TryGetProperty("message", out var msg) && msg.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in tc.EnumerateArray())
                    {
                        try
                        {
                            var id = t.GetProperty("id").GetString() ?? string.Empty;
                            var name = t.GetProperty("function").GetProperty("name").GetString() ?? string.Empty;
                            var argString = t.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";
                            var argsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(argString) ?? new();
                            toolCalls.Add(new ToolCall(id, name, argsDict));
                        }
                        catch { }
                    }
                }
                return new CompletionResponse(finalContent, toolCalls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse completion response");
                return CompletionResponse.Empty;
            }
        }

        private static string ExtractMessageContent(JsonElement choiceElement)
        {
            if (choiceElement.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentEl))
            {
                return contentEl.GetString() ?? string.Empty;
            }
            if (choiceElement.TryGetProperty("text", out var textEl))
            {
                return textEl.GetString() ?? string.Empty;
            }
            return string.Empty;
        }
        #endregion

        #region Parsing answer JSON
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
                var answer = root.TryGetProperty("answer", out var answerProp) ? answerProp.GetString() ?? string.Empty : answerPayload;
                var citations = new List<DocumentAnswerCitation>();
                var documents = new List<DocumentAnswerReference>();
                long? documentCount = null;

                if (root.TryGetProperty("citations", out var citationsProp) && citationsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var citation in citationsProp.EnumerateArray())
                    {
                        var chunkId = citation.TryGetProperty("chunk_id", out var chunkIdProp) ? chunkIdProp.GetString() : null;
                        var quote = citation.TryGetProperty("quote", out var quoteProp) ? quoteProp.GetString() : null;
                        var key = chunkId ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(key) && chunkMap.TryGetValue(key, out var chunk))
                        {
                            citations.Add(new DocumentAnswerCitation
                            {
                                Source = key,
                                Snippet = string.IsNullOrWhiteSpace(quote) ? TrimSnippet(chunk.Content) : quote ?? string.Empty
                            });
                        }
                        else if (!string.IsNullOrWhiteSpace(quote))
                        {
                            citations.Add(new DocumentAnswerCitation { Source = chunkId, Snippet = quote ?? string.Empty });
                        }
                    }
                }

                if (root.TryGetProperty("documents", out var documentsProp) && documentsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var docEntry in documentsProp.EnumerateArray())
                    {
                        if (docEntry.ValueKind != JsonValueKind.Object) continue;
                        if (!docEntry.TryGetProperty("id", out var idProp)) continue;
                        var idString = idProp.GetString();
                        if (!Guid.TryParse(idString, out var docId)) continue;
                        double? score = null;
                        if (docEntry.TryGetProperty("score", out var scoreProp))
                        {
                            if (scoreProp.ValueKind == JsonValueKind.Number) score = scoreProp.GetDouble();
                            else if (scoreProp.ValueKind == JsonValueKind.String && double.TryParse(scoreProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) score = s;
                        }
                        long? sizeBytes = null;
                        if (docEntry.TryGetProperty("file_size_bytes", out var sizeProp))
                        {
                            if (sizeProp.ValueKind == JsonValueKind.Number && sizeProp.TryGetInt64(out var sz)) sizeBytes = sz;
                            else if (sizeProp.ValueKind == JsonValueKind.String && long.TryParse(sizeProp.GetString(), out var sz2)) sizeBytes = sz2;
                        }
                        DateTime? date = null;
                        if (docEntry.TryGetProperty("date", out var dateProp) && dateProp.ValueKind == JsonValueKind.String && DateTime.TryParse(dateProp.GetString(), null, DateTimeStyles.RoundtripKind, out var d))
                        {
                            date = d;
                        }
                        documents.Add(new DocumentAnswerReference
                        {
                            DocumentId = docId,
                            Title = docEntry.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null,
                            Summary = docEntry.TryGetProperty("summary", out var summaryProp) ? summaryProp.GetString() : null,
                            Date = date,
                            Score = score,
                            FileSizeBytes = sizeBytes
                        });
                    }
                }

                if (root.TryGetProperty("document_count", out var countProp))
                {
                    if (countProp.ValueKind == JsonValueKind.Number && countProp.TryGetInt64(out var cnt)) documentCount = cnt;
                    else if (countProp.ValueKind == JsonValueKind.String && long.TryParse(countProp.GetString(), out var cnt2)) documentCount = cnt2;
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
            catch
            {
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
        #endregion

        #region Tool & query parsing
        private static object CreateChunkTool() => new
        {
            type = "function",
            function = new
            {
                name = LoadChunkToolName,
                description = "Loads the full text for one of the document chunks listed in the metadata.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        chunk_id = new { type = "string", description = "Identifier of the chunk to load" }
                    },
                    required = new[] { "chunk_id" }
                }
            }
        };

        private static object CreateDocumentQueryTool() => new
        {
            type = "function",
            function = new
            {
                name = QueryDocumentsToolName,
                description = "Searches or filters the user's document catalogue and returns identifiers, metadata, or counts.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["search"] = new { type = "string", description = "Optional full-text query" },
                        ["limit"] = new { type = "integer", minimum = 1, maximum = 20 },
                        ["projection"] = new { type = "string", @enum = new[] { "documents", "count", "both" } },
                        ["filters"] = new {
                            type = "object",
                            properties = new Dictionary<string, object>
                            {
                                ["document_ids"] = new { type = "array", items = new { type = "string" } },
                                ["year"] = new { type = "integer" },
                                ["min_file_size_mb"] = new { type = "number" },
                                ["max_file_size_mb"] = new { type = "number" },
                                ["type"] = new { type = "string" }
                            }
                        }
                    }
                }
            }
        };

        private static DocumentQuery? TryParseDocumentQuery(Dictionary<string, string> args)
        {
            try
            {
                var limit = 10;
                if (args.TryGetValue("limit", out var limitRaw) && int.TryParse(limitRaw, out var parsedLimit)) limit = parsedLimit;
                var projection = DocumentQueryProjection.Documents;
                if (args.TryGetValue("projection", out var projRaw) && !string.IsNullOrWhiteSpace(projRaw))
                {
                    projection = projRaw.Trim().ToLowerInvariant() switch
                    {
                        "count" => DocumentQueryProjection.Count,
                        "both" => DocumentQueryProjection.Both,
                        _ => DocumentQueryProjection.Documents
                    };
                }
                // filters not fully parsed from flat args (LLM uses structured JSON normally)
                var filters = new DocumentQueryFilters();
                var search = args.TryGetValue("search", out var s) ? s : null;
                return new DocumentQuery
                {
                    Search = string.IsNullOrWhiteSpace(search) ? null : search,
                    Filters = filters,
                    Projection = projection,
                    Limit = Math.Clamp(limit, 1, 20)
                };
            }
            catch { return null; }
        }
        #endregion

        #region Chunking & metadata
        private static Dictionary<string, DocumentChunk> SplitIntoChunks(string content)
        {
            var dict = new Dictionary<string, DocumentChunk>();
            if (string.IsNullOrEmpty(content)) return dict;
            const int size = 1200; int index = 0; int pos = 0;
            while (pos < content.Length)
            {
                var len = Math.Min(size, content.Length - pos);
                var slice = content.Substring(pos, len);
                var id = $"chunk_{++index}";
                dict[id] = new DocumentChunk(id, slice, index, pos, pos + len);
                pos += len;
            }
            return dict;
        }

        private static string BuildMetadata(DocumentQuestionContext ctx, IEnumerable<DocumentChunk> chunks)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Document metadata:");
            if (!string.IsNullOrWhiteSpace(ctx.Title)) sb.AppendLine($"- Title: {ctx.Title}");
            if (!string.IsNullOrWhiteSpace(ctx.Summary)) sb.AppendLine($"- Summary: {ctx.Summary}");
            if (ctx.Keywords?.Count > 0) sb.AppendLine($"- Keywords: {string.Join(", ", ctx.Keywords)}");
            if (!string.IsNullOrWhiteSpace(ctx.Language)) sb.AppendLine($"- Language: {ctx.Language}");
            if (ctx.History?.Count > 0)
            {
                sb.AppendLine("- Recent history:");
                foreach (var h in ctx.History.Take(5)) sb.AppendLine("  * " + h);
            }
            var ordered = chunks.OrderBy(c => c.Index).ToList();
            sb.AppendLine();
            sb.AppendLine($"Available document chunks ({ordered.Count}). Use load_document_chunk to read them:");
            foreach (var c in ordered) sb.AppendLine($"- {c.Id}: chars {c.StartPosition + 1}-{c.EndPosition}");
            sb.AppendLine();
            sb.AppendLine("Use query_documents for catalogue or statistics.");
            return sb.ToString();
        }

        private static string TrimSnippet(string content)
            => string.IsNullOrWhiteSpace(content) ? string.Empty : (content.Length <= 400 ? content : content.Substring(0, 400) + "...");
        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
        #endregion

        #region Small models
        private sealed record DocumentChunk(string Id, string Content, int Index, int StartPosition, int EndPosition);
        private sealed record ToolCall(string Id, string Name, Dictionary<string, string> Arguments);
        private sealed class CompletionResponse
        {
            public string? FinalContent { get; }
            public List<ToolCall> ToolCalls { get; }
            public CompletionResponse(string? content, List<ToolCall> calls) { FinalContent = content; ToolCalls = calls; }
            public static CompletionResponse Empty { get; } = new CompletionResponse(string.Empty, new List<ToolCall>());
        }
        #endregion
    }
}
