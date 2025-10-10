using ArquivoMate2.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.IngestionProvider
{
    /// <summary>
    /// No-op ingestion provider used when ingestion is disabled in configuration.
    /// </summary>
    public class NullIngestionProvider : IIngestionProvider
    {
        private readonly ILogger<NullIngestionProvider> _logger;

        public NullIngestionProvider(ILogger<NullIngestionProvider> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// No configured ingestion email for the null provider.
        /// </summary>
        public string? IngestionEmailAddress => null;

        public Task<IReadOnlyList<IngestionFileDescriptor>> ListPendingFilesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("ListPendingFilesAsync called on NullIngestionProvider");
            return Task.FromResult<IReadOnlyList<IngestionFileDescriptor>>(Array.Empty<IngestionFileDescriptor>());
        }

        public Task MarkFailedAsync(IngestionFileDescriptor descriptor, string? reason, CancellationToken cancellationToken)
        {
            _logger.LogWarning("MarkFailedAsync invoked on NullIngestionProvider for {File}", descriptor.FullPath);
            return Task.CompletedTask;
        }

        public Task MarkProcessedAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            _logger.LogWarning("MarkProcessedAsync invoked on NullIngestionProvider for {File}", descriptor.FullPath);
            return Task.CompletedTask;
        }

        public Task<string> SaveIncomingFileAsync(string userId, string fileName, Stream content, CancellationToken cancellationToken)
        {
            _logger.LogError("SaveIncomingFileAsync invoked on NullIngestionProvider. Ingestion is not configured.");
            throw new InvalidOperationException("Ingestion provider is not configured.");
        }

        public Task<byte[]> ReadFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            _logger.LogError("ReadFileAsync invoked on NullIngestionProvider for {File}.", descriptor.FullPath);
            throw new InvalidOperationException("Ingestion provider is not configured.");
        }
    }
}
