using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using OpenAI.Batch;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public class OpenAIBatchChatBot : IChatBot
    {
        private readonly BatchClient _client;

            public OpenAIBatchChatBot(BatchClient client, string serverLanguage)
        {
            _client = client;
                _ = serverLanguage; // parameter kept for parity with other bots
        }

        public string ModelName => string.Empty;

        public Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, IReadOnlyList<DocumentTypeOption> availableTypes, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<DocumentAnswerResult> AnswerQuestion(DocumentQuestionContext context, string question, IDocumentQuestionTooling tooling, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Document question answering is not supported in batch mode.");
        }

        public Task<DocumentAnswerResult> AnswerQuestionWithPrompt(string question, string documentContent, string? structuredJsonSchema, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Document question answering is not supported in batch mode.");
        }

        public Task<DocumentAnswerResult> AnswerQuestionWithPrompt(string question, byte[]? imageBytes, string? imageContentType, string? structuredJsonSchema, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Document question answering with images is not supported in batch mode.");
        }
    }
}
