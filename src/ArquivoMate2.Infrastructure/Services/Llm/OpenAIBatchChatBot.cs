using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using OpenAI.Batch;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public class OpenAIBatchChatBot : IChatBot
    {
        private readonly BatchClient _client;

        public OpenAIBatchChatBot(BatchClient client)
        {
            _client = client;
        }

        public string ModelName => "";

        public Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
