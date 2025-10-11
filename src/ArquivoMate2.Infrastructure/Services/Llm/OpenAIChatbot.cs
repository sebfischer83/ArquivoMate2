using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public class OpenAIChatBot : IChatBot
    {
        private readonly ChatClient _client;

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

        public async Task<DocumentAnswerResult> AnswerQuestion(DocumentQuestionContext context, string question, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                throw new ArgumentException("Question must not be empty", nameof(question));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var chunkMap = SplitIntoChunks(context.Content);
            var metadata = BuildMetadataSection(context, chunkMap.Values);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a helpful assistant answering questions about the provided document. Use the available tool to fetch any chunk you need before answering. Answer truthfully using only fetched chunks; if the answer is unknown, say so. Return the final response as compact JSON with fields 'answer' and 'citations' (an array of objects with 'chunk_id' and optional 'quote')."),
                new UserChatMessage(metadata + "\nQuestion: " + question.Trim())
            };

            var chunkTool = ChatTool.CreateFunctionTool(
                functionName: "load_document_chunk",
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

            var options = new ChatCompletionOptions
            {
                ToolChoice = ChatToolChoice.CreateAutoChoice()
            };
            options.Tools.Add(chunkTool);

            ChatCompletion response = await _client.CompleteChatAsync(messages, options, cancellationToken);

            var iterations = 0;
            const int maxIterations = 5;

            while (response.ToolCalls.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                foreach (var toolCall in response.ToolCalls.OfType<ChatFunctionToolCall>())
                {
                    var chunkId = TryGetChunkId(toolCall);
                    var chunkContent = chunkId != null && chunkMap.TryGetValue(chunkId, out var chunk)
                        ? chunk.Content
                        : "";

                    var toolPayload = JsonSerializer.Serialize(new
                    {
                        chunk_id = chunkId,
                        content = chunkContent
                    });

                    messages.Add(new ToolChatMessage(toolCall.Id, toolPayload));
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
                    Citations = Array.Empty<DocumentAnswerCitation>()
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

                return new DocumentAnswerResult
                {
                    Answer = answer,
                    Model = modelName,
                    Citations = citations
                };
            }
            catch (JsonException)
            {
                // Fallback to plain text if parsing fails.
                return new DocumentAnswerResult
                {
                    Answer = answerPayload,
                    Model = modelName,
                    Citations = Array.Empty<DocumentAnswerCitation>()
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

            return builder.ToString();
        }

        private sealed record DocumentChunk(string Id, string Content, int Index, int StartPosition, int EndPosition);
    }
}
