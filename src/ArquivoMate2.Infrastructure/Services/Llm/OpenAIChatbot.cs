using ArquivoMate2.Application.Interfaces;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ArquivoMate2.Application.Models;
using Newtonsoft.Json.Schema;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                    new SystemChatMessage($"You are an assistant that analyzes the document and ALWAYS returns JSON according to the defined schema.Respond in German. " +
                    $"Suggest maximum of 5 keywords. The summary should not exceed 500 characters.Let fields empty if you can't fill them. " + 
                    "The title should be a very short description of the content."),
                    new UserChatMessage($"Document text:\n{content}")
                };

            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "analyze_document",
                    jsonSchema: BinaryData.FromString(OpenAIHelper.SchemaJson),
                    jsonSchemaIsStrict: true)
            };

            // Call the API
            ChatCompletion response = await _client.CompleteChatAsync(messages, options);

            // Extract JSON text
            string jsonText = response.Content[0].Text;

            // Deserialize JSON into DocumentAnalysisResult
            return JsonSerializer.Deserialize<DocumentAnalysisResult>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }
    }
}
