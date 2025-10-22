using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    /// <summary>
    /// No-op chat bot used as a fallback when no chatbot is configured.
    /// Methods return empty results and never throw.
    /// </summary>
    public class NullChatBot : IChatBot
    {
        public string ModelName => string.Empty;

        public Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, IReadOnlyList<DocumentTypeOption> availableTypes, CancellationToken cancellationToken)
        {
            var empty = new DocumentAnalysisResult();
            return Task.FromResult(empty);
        }

        public Task<T> AnalyzeDocumentFile<T>(byte[] imageBytes, string contentType, string question, string systemPrompt, string? structuredJsonSchema, CancellationToken cancellationToken)
        {
           return Task.FromResult(default(T)!);
        }

        public Task<DocumentAnswerResult> AnswerQuestion(DocumentQuestionContext context, string question, IDocumentQuestionTooling tooling, CancellationToken cancellationToken)
        {
            var result = new DocumentAnswerResult
            {
                Answer = string.Empty,
                Model = string.Empty,
                Citations = Array.Empty<DocumentAnswerCitation>(),
                Documents = Array.Empty<DocumentAnswerReference>(),
                DocumentCount = null
            };
            return Task.FromResult(result);
        }

        public Task<DocumentAnswerResult> AnswerQuestionWithPrompt(string question, string documentContent, string? structuredJsonSchema, CancellationToken cancellationToken)
        {
            var result = new DocumentAnswerResult
            {
                Answer = string.Empty,
                Model = string.Empty,
                Citations = Array.Empty<DocumentAnswerCitation>(),
                Documents = Array.Empty<DocumentAnswerReference>(),
                DocumentCount = null
            };
            return Task.FromResult(result);
        }

        public Task<DocumentAnswerResult> AnswerQuestionWithPrompt(string question, byte[]? imageBytes, string? imageContentType, string? structuredJsonSchema, CancellationToken cancellationToken)
        {
            // Null bot cannot inspect images; return empty result
            var result = new DocumentAnswerResult
            {
                Answer = string.Empty,
                Model = string.Empty,
                Citations = Array.Empty<DocumentAnswerCitation>(),
                Documents = Array.Empty<DocumentAnswerReference>(),
                DocumentCount = null
            };
            return Task.FromResult(result);
        }
    }
}
