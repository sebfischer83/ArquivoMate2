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

        public async Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, CancellationToken cancellationToken)
        {
            var schemaJson = """
    {
        "type": "object",
        "properties": {
            "date": { "type": "string" },
            "documentType": { "type": "string" },
            "sender": {
                "type": "object",
                "properties": {
                    "firstName": { "type": "string" },
                    "lastName": { "type": "string" },
                    "companyName": { "type": "string" },
                    "street": { "type": "string" },
                    "houseNumber": { "type": "string" },
                    "postalCode": { "type": "string" },
                    "city": { "type": "string" }
                },            "required": ["firstName","lastName","companyName","street","houseNumber","postalCode","city"],
                "additionalProperties": false
            },
            "recipient": {
                "type": "object",
                "properties": {
                    "firstName": { "type": "string" },
                    "lastName": { "type": "string" },
                    "companyName": { "type": "string" },
                    "street": { "type": "string" },
                    "houseNumber": { "type": "string" },
                    "postalCode": { "type": "string" },
                    "city": { "type": "string" }
                },
                "required": ["firstName","lastName","companyName","street","houseNumber","postalCode","city"],
                "additionalProperties": false
            },
            "customerNumber": { "type": "string" },
            "invoiceNumber": { "type": "string" },
            "totalPrice": { "type": "number" },
            "keywords": {
                "type": "array",
                "items": { "type": "string" }
            },
            "summary": { "type": "string" }
        },
        "required": ["date","documentType","sender","recipient","customerNumber","invoiceNumber","totalPrice","keywords","summary"],
        "additionalProperties": false
    }
    """;

            var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are an assistant that analyzes the document and ALWAYS returns JSON according to the defined schema.Respond in de."),
                    new UserChatMessage($"Document text:\n{content}")
                };

            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "analyze_document",
                    jsonSchema: BinaryData.FromString(schemaJson),
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
