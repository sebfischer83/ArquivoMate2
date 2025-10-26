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
using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ArquivoMate2.Application.Handlers.Documents
{
    public class SetDocumentAcceptedHandler : IRequestHandler<SetDocumentAcceptedCommand, bool>
    {
        private static readonly ActivitySource ActivitySource = new("ArquivoMate2.Application.SetDocumentAcceptedHandler");

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
            using var rootActivity = ActivitySource.StartActivity("SetDocumentAccepted.Handle", ActivityKind.Internal);
            if (rootActivity != null)
            {
                rootActivity.SetTag("document.id", request.DocumentId.ToString());
                rootActivity.SetTag("user.id", request.UserId ?? string.Empty);
                rootActivity.SetTag("document.accepted", request.Accepted);
            }

            // Root scope for correlation and easy filtering
            using var rootScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["DocumentId"] = request.DocumentId,
                ["UserId"] = request.UserId ?? string.Empty,
                ["Accepted"] = request.Accepted
            });

            _logger.LogInformation("[SetDocumentAccepted] Start handling command");

            // Ensure document exists
            using (_logger.BeginScope("LoadDocument"))
            using (var loadActivity = ActivitySource.StartActivity("LoadDocument", ActivityKind.Internal))
            {
                _logger.LogDebug("Aggregating event stream for document");
                loadActivity?.SetTag("document.id", request.DocumentId.ToString());
                var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId);
                _logger.LogDebug("Document loaded: Present={Present}", doc != null);
                loadActivity?.SetTag("document.present", doc != null);

                if (doc == null) return false;

                using (_logger.BeginScope("ValidateProcessed"))
                using (var validateActivity = ActivitySource.StartActivity("ValidateProcessed", ActivityKind.Internal))
                {
                    validateActivity?.SetTag("document.processed", doc.Processed);
                    if (!doc.Processed)
                    {
                        // Business rule: cannot accept an unprocessed document
                        _logger.LogWarning("Attempt to accept unprocessed document {DocumentId}", request.DocumentId);
                        validateActivity?.SetStatus(ActivityStatusCode.Error, "Document not processed");
                        throw new InvalidOperationException("Document must be processed before it can be accepted.");
                    }
                }

                // Append event and save
                using (_logger.BeginScope("AppendEvent"))
                using (var appendActivity = ActivitySource.StartActivity("AppendEvent", ActivityKind.Internal))
                {
                    appendActivity?.SetTag("document.accepted", request.Accepted);
                    _logger.LogDebug("Appending DocumentAccepted event (Accepted={Accepted})", request.Accepted);
                    var @event = new DocumentAccepted(request.DocumentId, request.Accepted, DateTime.UtcNow);
                    _session.Events.Append(request.DocumentId, @event);
                    await _session.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("DocumentAccepted event persisted for document {DocumentId}", request.DocumentId);
                    appendActivity?.SetTag("event.persisted", true);
                }

                _logger.LogInformation("User {UserId} set Accepted={Accepted} for document {DocumentId}", request.UserId, request.Accepted, request.DocumentId);

                // Feature trigger only on Accepted == true
                if (request.Accepted)
                {
                    using (_logger.BeginScope("FeatureScheduling"))
                    using (var featureActivity = ActivitySource.StartActivity("FeatureScheduling", ActivityKind.Internal))
                    {
                        try
                        {
                            featureActivity?.SetTag("document.type", doc.Type ?? string.Empty);
                            _logger.LogDebug("Resolving document type definition for feature scheduling");
                            // Resolve document type definition
                            var docTypeName = doc.Type;
                            _logger.LogDebug("Document type: {DocType}", docTypeName);
                            if (!string.IsNullOrWhiteSpace(docTypeName))
                            {
                                var definition = await _querySession.Query<DocumentTypeDefinition>()
                                    .FirstOrDefaultAsync(x => x.Name.Equals(docTypeName, StringComparison.OrdinalIgnoreCase), cancellationToken);

                                // use first configured system feature if any
                                var featureKey = definition?.SystemFeatures?.FirstOrDefault();
                                featureActivity?.SetTag("feature.key", featureKey ?? string.Empty);
                                _logger.LogDebug("Resolved featureKey: {FeatureKey}", featureKey);
                                if (!string.IsNullOrWhiteSpace(featureKey))
                                {
                                    var processor = _registry.Get(featureKey!);
                                    if (processor != null)
                                    {
                                        // Idempotenz prüfen
                                        _logger.LogDebug("Checking for existing DocumentFeatureProcessing status");
                                        var existing = await _querySession.Query<DocumentFeatureProcessing>()
                                            .FirstOrDefaultAsync(x => x.DocumentId == request.DocumentId, cancellationToken);
                                        featureActivity?.SetTag("existing.processing", existing != null);
                                        _logger.LogDebug("Existing processing status found: {Found}", existing != null);
                                        if (existing == null)
                                        {
                                            var chatBot = _services.GetService(typeof(IChatBot)) as IChatBot;
                                            var status = new DocumentFeatureProcessing
                                            {
                                                DocumentId = request.DocumentId,
                                                FeatureKey = featureKey!,
                                                ChatBotAvailable = chatBot != null,
                                                State = FeatureProcessingState.Pending,
                                                CreatedAtUtc = DateTime.UtcNow
                                            };
                                            _session.Store(status);
                                            await _session.SaveChangesAsync(cancellationToken);
                                            _logger.LogInformation("Stored DocumentFeatureProcessing status for document {DocumentId} feature {FeatureKey}", request.DocumentId, featureKey);
                                            // Schedule background job
                                            BackgroundJob.Enqueue<SystemFeatureProcessingJob>(job => job.ExecuteAsync(request.DocumentId, featureKey!));
                                            _logger.LogInformation("Scheduled feature processing job for document {DocumentId} with feature {FeatureKey}", request.DocumentId, featureKey);
                                            featureActivity?.SetTag("scheduled", true);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("No processor found for feature key {FeatureKey}", featureKey);
                                        featureActivity?.SetTag("processor.found", false);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            featureActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                            _logger.LogError(ex, "Failed scheduling feature processing for document {DocumentId}", request.DocumentId);
                        }
                    }
                }

                return true;
            }
        }
    }
}
