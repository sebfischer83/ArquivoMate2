using System;
using System.Threading.Tasks;
using ArquivoMate2.Domain.Features;
using ArquivoMate2.Application.Features;
using ArquivoMate2.Application.Interfaces;
using Marten;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.Application.Jobs
{
    public class SystemFeatureProcessingJob
    {
        private readonly IQuerySession _querySession;
        private readonly IDocumentSession _documentSession;
        private readonly ISystemFeatureProcessorRegistry _registry;
        private readonly IServiceProvider _services;
        private readonly ILogger<SystemFeatureProcessingJob> _logger;

        public SystemFeatureProcessingJob(IQuerySession querySession,
            IDocumentSession documentSession,
            ISystemFeatureProcessorRegistry registry,
            IServiceProvider services,
            ILogger<SystemFeatureProcessingJob> logger)
        {
            _querySession = querySession;
            _documentSession = documentSession;
            _registry = registry;
            _services = services;
            _logger = logger;
        }

        public async Task ExecuteAsync(Guid documentId, string featureKey)
        {
            var status = await _querySession.Query<DocumentFeatureProcessing>()
                .FirstOrDefaultAsync(x => x.DocumentId == documentId);
            if (status == null)
            {
                _logger.LogWarning("Feature status document missing for {DocumentId}", documentId);
                return;
            }

            if (status.State == FeatureProcessingState.Completed)
            {
                _logger.LogInformation("Feature processing already completed for {DocumentId}", documentId);
                return;
            }

            var processor = _registry.Get(featureKey);
            if (processor == null)
            {
                _logger.LogWarning("No processor registered for feature {FeatureKey}", featureKey);
                status.State = FeatureProcessingState.Failed;
                status.LastError = $"Processor missing for feature {featureKey}";
                status.FailedAtUtc = DateTime.UtcNow;
                _documentSession.Store(status);
                await _documentSession.SaveChangesAsync();
                return;
            }

            status.State = FeatureProcessingState.Running;
            status.StartedAtUtc = DateTime.UtcNow;
            _documentSession.Store(status);
            await _documentSession.SaveChangesAsync();

            var chatBot = _services.GetService(typeof(IChatBot)) as IChatBot;
            var context = new SystemFeatureProcessingContext
            {
                DocumentId = documentId,
                FeatureKey = featureKey,
                ChatBot = chatBot
            };

            try
            {
                await processor.ProcessAsync(context, default);
                status.State = FeatureProcessingState.Completed;
                status.CompletedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feature processing failed for document {DocumentId} feature {FeatureKey}", documentId, featureKey);
                status.State = FeatureProcessingState.Failed;
                status.LastError = ex.Message;
                status.FailedAtUtc = DateTime.UtcNow;
                status.AttemptCount += 1;
            }

            _documentSession.Store(status);
            await _documentSession.SaveChangesAsync();
        }
    }
}
