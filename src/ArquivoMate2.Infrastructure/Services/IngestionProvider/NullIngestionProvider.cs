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

        /// <summary>
        /// Initializes a NullIngestionProvider that performs no ingestion operations and uses the provided logger for diagnostic messages.
        /// </summary>
        public NullIngestionProvider(ILogger<NullIngestionProvider> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Provide an empty collection of pending ingestion files when ingestion is not configured.
        /// </summary>
        /// <returns>An empty read-only list of <see cref="IngestionFileDescriptor"/>.</returns>
        public Task<IReadOnlyList<IngestionFileDescriptor>> ListPendingFilesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("ListPendingFilesAsync called on NullIngestionProvider");
            return Task.FromResult<IReadOnlyList<IngestionFileDescriptor>>(Array.Empty<IngestionFileDescriptor>());
        }

        /// <summary>
        /// Records that the given ingestion file has failed in this no-op provider; logs a warning and does not persist any state.
        /// </summary>
        /// <param name="descriptor">Descriptor of the ingestion file that is being marked as failed.</param>
        /// <param name="reason">Optional human-readable reason for the failure.</param>
        public Task MarkFailedAsync(IngestionFileDescriptor descriptor, string? reason, CancellationToken cancellationToken)
        {
            _logger.LogWarning("MarkFailedAsync invoked on NullIngestionProvider for {File}", descriptor.FullPath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Marks the provided ingestion file descriptor as processed; in this null implementation the operation is a no-op and only logs a warning.
        /// </summary>
        /// <param name="descriptor">The descriptor of the file that would be marked as processed.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        /// <returns>A task that completes when the operation has finished.</returns>
        public Task MarkProcessedAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            _logger.LogWarning("MarkProcessedAsync invoked on NullIngestionProvider for {File}", descriptor.FullPath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Indicates that saving an incoming file is unsupported because ingestion is not configured.
        /// </summary>
        /// <param name="userId">The ID of the user who uploaded the file.</param>
        /// <param name="fileName">The original name of the uploaded file.</param>
        /// <param name="content">The stream containing the file content.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The identifier or path of the saved file.</returns>
        /// <exception cref="InvalidOperationException">Always thrown with message "Ingestion provider is not configured."</exception>
        public Task<string> SaveIncomingFileAsync(string userId, string fileName, Stream content, CancellationToken cancellationToken)
        {
            _logger.LogError("SaveIncomingFileAsync invoked on NullIngestionProvider. Ingestion is not configured.");
            throw new InvalidOperationException("Ingestion provider is not configured.");
        }

        /// <summary>
        /// Attempts to read the contents of the specified ingestion file but throws when the ingestion provider is not configured.
        /// </summary>
        /// <param name="descriptor">Descriptor of the ingestion file to read.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        /// <returns>A byte array containing the file's contents.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the ingestion provider is not configured.</exception>
        public Task<byte[]> ReadFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            _logger.LogError("ReadFileAsync invoked on NullIngestionProvider for {File}.", descriptor.FullPath);
            throw new InvalidOperationException("Ingestion provider is not configured.");
        }
    }
}