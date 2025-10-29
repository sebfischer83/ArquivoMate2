using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ArquivoMate2.Application.Commands.Features;
using Marten;
using ArquivoMate2.Domain.Features;
using ArquivoMate2.Application.Jobs;
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using ArquivoMate2.Application.Features;

namespace ArquivoMate2.Application.Handlers.Features
{
    public class RestartDocumentFeatureProcessingHandler : IRequestHandler<RestartDocumentFeatureProcessingCommand, bool>
    {
        private readonly IQuerySession _query;
        private readonly IDocumentSession _documentSession;
        private readonly ISystemFeatureProcessorRegistry _registry;
        private readonly ILogger<RestartDocumentFeatureProcessingHandler> _logger;

        public RestartDocumentFeatureProcessingHandler(IQuerySession query, IDocumentSession documentSession, ISystemFeatureProcessorRegistry registry, ILogger<RestartDocumentFeatureProcessingHandler> logger)
        {
            _query = query;
            _documentSession = documentSession;
            _registry = registry;
            _logger = logger;
        }

        public async Task<bool> Handle(RestartDocumentFeatureProcessingCommand request, CancellationToken cancellationToken)
        {
            var status = await _query.Query<DocumentFeatureProcessing>()
                .FirstOrDefaultAsync(x => x.DocumentId == request.DocumentId && x.FeatureKey == request.FeatureKey, cancellationToken);

            if (status == null)
            {
                _logger.LogWarning("No feature status found for document {DocumentId} feature {FeatureKey}", request.DocumentId, request.FeatureKey);
                return false;
            }

            if (status.State == FeatureProcessingState.Running)
            {
                _logger.LogInformation("Feature processing is already running for document {DocumentId}", request.DocumentId);
                return false;
            }

            // reset failure state and schedule job again
            status.State = FeatureProcessingState.Pending;
            status.FailedAtUtc = null;
            status.LastError = null;
            _documentSession.Store(status);
            await _documentSession.SaveChangesAsync(cancellationToken);

            BackgroundJob.Enqueue<SystemFeatureProcessingJob>(job => job.ExecuteAsync(request.DocumentId, request.FeatureKey));
            _logger.LogInformation("Re-scheduled feature processing for document {DocumentId} feature {FeatureKey}", request.DocumentId, request.FeatureKey);

            return true;
        }
    }
}
