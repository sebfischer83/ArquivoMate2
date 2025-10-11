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

            var userPromptBuilder = new StringBuilder();
            userPromptBuilder.AppendLine("You will receive a document context and a question. Use only the provided content to answer.");
            userPromptBuilder.AppendLine();
            userPromptBuilder.AppendLine("[Document Metadata]");
            if (!string.IsNullOrWhiteSpace(context.Title))
            {
                userPromptBuilder.AppendLine($"Title: {context.Title}");
            }
            if (!string.IsNullOrWhiteSpace(context.Summary))
            {
                userPromptBuilder.AppendLine($"Summary: {context.Summary}");
            }
            if (context.Keywords?.Count > 0)
            {
                userPromptBuilder.AppendLine($"Keywords: {string.Join(", ", context.Keywords)}");
            }
            if (!string.IsNullOrWhiteSpace(context.Language))
            {
                userPromptBuilder.AppendLine($"Language: {context.Language}");
            }

            if (context.History?.Count > 0)
            {
                userPromptBuilder.AppendLine();
                userPromptBuilder.AppendLine("[Recent History]");
                foreach (var entry in context.History.Take(5))
                {
                    userPromptBuilder.AppendLine(entry);
                }
            }

            userPromptBuilder.AppendLine();
            userPromptBuilder.AppendLine("[Document Content]");
            userPromptBuilder.AppendLine(context.Content);
            userPromptBuilder.AppendLine();
            userPromptBuilder.AppendLine("[User Question]");
            userPromptBuilder.AppendLine(question.Trim());

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a helpful assistant answering questions about the provided document. Answer truthfully using only the supplied context. If the answer cannot be derived, respond that the information is not available. Prefer replying in the user's language when possible."),
                new UserChatMessage(userPromptBuilder.ToString())
            };

            ChatCompletion response = await _client.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            var answerText = response.Content.FirstOrDefault()?.Text?.Trim() ?? string.Empty;

            return new DocumentAnswerResult
            {
                Answer = answerText,
                Model = ModelName,
                Citations = Array.Empty<DocumentAnswerCitation>()
            };
        }
    }
}
