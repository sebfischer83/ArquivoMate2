using ArquivoMate2.Domain.Document;
using Marten;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.Application.Features.Processors
{
    public class LabResultsFeatureProcessor : ISystemFeatureProcessor
    {
        private readonly IQuerySession _query;
        private readonly IDocumentSession _session;
        private readonly ILogger<LabResultsFeatureProcessor> _logger;
        public string FeatureKey => "lab-results";

        public LabResultsFeatureProcessor(IQuerySession query, IDocumentSession session, ILogger<LabResultsFeatureProcessor> logger)
        {
            _query = query;
            _session = session;
            _logger = logger;
        }

        public async Task ProcessAsync(SystemFeatureProcessingContext context, CancellationToken ct)
        {
            _logger.LogInformation("[LabResults] Start processing document {DocumentId}, ChatBotAvailable={ChatBotAvailable}", context.DocumentId, context.ChatBotAvailable);

            if (!context.ChatBotAvailable)
            {
                _logger.LogWarning("[LabResults] Chatbot needed for this Feature.");
                return; // Mark job completed without data
            }

            var docView = await _query.Events.AggregateStreamAsync<Document>(context.DocumentId, token: ct);
            if (docView == null)
            {
                _logger.LogWarning("[LabResults] Document {DocumentId} nicht gefunden.", context.DocumentId);
                return; // Mark job completed without data
            }

            


            _logger.LogInformation("[LabResults] Completed processing document {DocumentId}", context.DocumentId);
        }
    }
}
