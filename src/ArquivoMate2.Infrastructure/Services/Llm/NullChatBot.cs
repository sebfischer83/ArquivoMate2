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
    }
}
