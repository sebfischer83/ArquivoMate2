using ArquivoMate2.Application.Commands;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.DocumentTypes;
using ArquivoMate2.Domain.Features;
using ArquivoMate2.Application.Features;
using Hangfire;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Jobs;
using Marten;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Handlers.Documents
{
    public class SetDocumentAcceptedHandler : IRequestHandler<SetDocumentAcceptedCommand, bool>
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<SetDocumentAcceptedHandler> _logger;

        private readonly IQuerySession _querySession;
        private readonly ISystemFeatureProcessorRegistry _registry;
        private readonly IServiceProvider _services;

        public SetDocumentAcceptedHandler(IDocumentSession session,
            IQuerySession querySession,
            ISystemFeatureProcessorRegistry registry,
            IServiceProvider services,
            ILogger<SetDocumentAcceptedHandler> logger)
        {
            _session = session;
            _querySession = querySession;
            _registry = registry;
            _services = services;
            _logger = logger;
        }

        public async Task<bool> Handle(SetDocumentAcceptedCommand request, CancellationToken cancellationToken)
        {
            // Ensure document exists
            var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId);
            if (doc == null) return false;

            if (!doc.Processed)
            {
                // Business rule: cannot accept an unprocessed document
                throw new InvalidOperationException("Document must be processed before it can be accepted.");
            }

            var @event = new DocumentAccepted(request.DocumentId, request.Accepted, DateTime.UtcNow);
            _session.Events.Append(request.DocumentId, @event);
            await _session.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} set Accepted={Accepted} for document {DocumentId}", request.UserId, request.Accepted, request.DocumentId);

            // Feature trigger only on Accepted == true
            if (request.Accepted)
            {
                try
                {
                    // Resolve document type definition
                    var docTypeName = doc.Type;
                    if (!string.IsNullOrWhiteSpace(docTypeName))
                    {
                        var definition = await _querySession.Query<DocumentTypeDefinition>()
                            .FirstOrDefaultAsync(x => x.Name.Equals(docTypeName, StringComparison.OrdinalIgnoreCase), cancellationToken);
                        var featureKey = definition?.SystemFeature;
                        if (!string.IsNullOrWhiteSpace(featureKey))
                        {
                            var processor = _registry.Get(featureKey!);
                            if (processor != null)
                            {
                                // Idempotenz prüfen
                                var existing = await _querySession.Query<DocumentFeatureProcessing>()
                                    .FirstOrDefaultAsync(x => x.Id == request.DocumentId, cancellationToken);
                                if (existing == null)
                                {
                                    var chatBot = _services.GetService(typeof(IChatBot)) as IChatBot;
                                    var status = new DocumentFeatureProcessing
                                    {
                                        Id = request.DocumentId,
                                        FeatureKey = featureKey!,
                                        ChatBotAvailable = chatBot != null,
                                        State = FeatureProcessingState.Pending,
                                        CreatedAtUtc = DateTime.UtcNow
                                    };
                                    _session.Store(status);
                                    await _session.SaveChangesAsync(cancellationToken);
                                    // Schedule background job
                                    BackgroundJob.Enqueue<SystemFeatureProcessingJob>(job => job.ExecuteAsync(request.DocumentId, featureKey!));
                                    _logger.LogInformation("Scheduled feature processing job for document {DocumentId} with feature {FeatureKey}", request.DocumentId, featureKey);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed scheduling feature processing for document {DocumentId}", request.DocumentId);
                }
            }

            return true;
        }
    }
}
