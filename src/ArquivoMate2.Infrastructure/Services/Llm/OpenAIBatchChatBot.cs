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

        public OpenAIBatchChatBot(BatchClient client, string responseLanguage)
        {
            _client = client;
            _ = responseLanguage; // parameter kept for parity with other bots
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
    }
}
