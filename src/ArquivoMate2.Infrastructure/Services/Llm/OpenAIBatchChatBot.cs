using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using OpenAI.Batch;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public class OpenAIBatchChatBot : IChatBot
    {
        private BatchClient _client;

        public OpenAIBatchChatBot(BatchClient client)
        {
            _client = client;
        }

        public Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
