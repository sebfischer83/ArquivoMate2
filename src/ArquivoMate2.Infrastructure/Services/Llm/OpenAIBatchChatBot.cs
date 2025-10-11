using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using OpenAI.Batch;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public class OpenAIBatchChatBot : IChatBot
    {
        private readonly BatchClient _client;

        public OpenAIBatchChatBot(BatchClient client)
        {
            _client = client;
        }

        public string ModelName => string.Empty;

        public Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<DocumentAnswerResult> AnswerQuestion(DocumentQuestionContext context, string question, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Document question answering is not supported in batch mode.");
        }
    }
}
