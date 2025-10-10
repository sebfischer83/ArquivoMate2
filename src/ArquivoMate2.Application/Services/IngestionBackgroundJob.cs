using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Shared.Models;
using Hangfire;
using Marten;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Services
{
    /// <summary>
    /// Hangfire job that periodically inspects the configured ingestion provider
    /// for new files and enqueues them for document processing.
    /// </summary>
    public class IngestionBackgroundJob
    {
        private readonly IIngestionProvider _ingestionProvider;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<IngestionBackgroundJob> _logger;

        public IngestionBackgroundJob(IIngestionProvider ingestionProvider, IServiceScopeFactory serviceScopeFactory, ILogger<IngestionBackgroundJob> logger)
        {
            _ingestionProvider = ingestionProvider;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 600)]
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var descriptors = await _ingestionProvider.ListPendingFilesAsync(cancellationToken).ConfigureAwait(false);

            if (descriptors.Count == 0)
            {
                _logger.LogDebug("No pending ingestion files detected.");
                return;
            }

            _logger.LogInformation("Found {Count} pending ingestion files.", descriptors.Count);

            foreach (var descriptor in descriptors)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await ProcessSingleFileAsync(descriptor, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ProcessSingleFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

            _logger.LogInformation("Processing ingestion file {File} for user {UserId}.", descriptor.FileName, descriptor.UserId);

            try
            {
                var content = await _ingestionProvider.ReadFileAsync(descriptor, cancellationToken).ConfigureAwait(false);

                var subject = Path.GetFileNameWithoutExtension(descriptor.FileName);
                if (string.IsNullOrWhiteSpace(subject))
                {
                    subject = descriptor.FileName;
                }

                var uploadId = await mediator.Send(
                    new UploadDocumentByMailCommand(
                        descriptor.UserId,
                        new EmailDocument
                        {
                            Email = "ingestion@arquivomate2.local",
                            Subject = subject,
                            File = content,
                            FileName = descriptor.FileName
                        }),
                    cancellationToken).ConfigureAwait(false);

                var processedEvent = new InitDocumentImport(
                    Guid.NewGuid(),
                    descriptor.UserId,
                    descriptor.FileName,
                    DateTime.UtcNow,
                    ImportSource.Ingestion);

                session.Events.StartStream<ImportProcess>(processedEvent.AggregateId, processedEvent);
                await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                BackgroundJob.Enqueue<DocumentProcessingService>("documents", svc => svc.ProcessAsync(uploadId, processedEvent.AggregateId, descriptor.UserId));

                await _ingestionProvider.MarkProcessedAsync(descriptor, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Queued ingestion file {File} for processing as document {DocumentId}.", descriptor.FileName, uploadId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process ingestion file {File} for user {UserId}.", descriptor.FileName, descriptor.UserId);

                try
                {
                    await _ingestionProvider.MarkFailedAsync(descriptor, ex.Message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception markEx)
                {
                    _logger.LogError(markEx, "Failed to move ingestion file {File} to failed folder.", descriptor.FullPath);
                }
            }
        }
    }
}
