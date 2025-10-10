using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.IngestionProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.IngestionProvider
{
    /// <summary>
    /// Filesystem implementation of the ingestion provider. Files are dropped into
    /// user-specific directories under the configured root path. During pickup they
    /// are moved into a processing folder to avoid duplicate work. Successful and
    /// failed files are moved into their respective folders for auditing.
    /// </summary>
    public class FileSystemIngestionProvider : IIngestionProvider
    {
        private readonly FileSystemIngestionProviderSettings _settings;
        private readonly ILogger<FileSystemIngestionProvider> _logger;

        public FileSystemIngestionProvider(
            IOptions<FileSystemIngestionProviderSettings> options,
            ILogger<FileSystemIngestionProvider> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Optional email address used when creating an EmailDocument for ingestion sources.
        /// If not configured, null is returned.
        /// </summary>
        public string? IngestionEmailAddress => string.IsNullOrWhiteSpace(_settings.IngestionEmail) ? null : _settings.IngestionEmail;

        public Task<IReadOnlyList<IngestionFileDescriptor>> ListPendingFilesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(_settings.RootPath))
            {
                return Task.FromResult<IReadOnlyList<IngestionFileDescriptor>>(Array.Empty<IngestionFileDescriptor>());
            }

            var result = new List<IngestionFileDescriptor>();

            if (!Directory.Exists(_settings.RootPath))
            {
                Directory.CreateDirectory(_settings.RootPath);
                return Task.FromResult<IReadOnlyList<IngestionFileDescriptor>>(result);
            }

            foreach (var userDirectory in Directory.EnumerateDirectories(_settings.RootPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var userId = Path.GetFileName(userDirectory);
                if (string.IsNullOrEmpty(userId))
                {
                    continue;
                }

                var processingDirectory = EnsureDirectory(Path.Combine(userDirectory, _settings.ProcessingSubfolderName));

                // Requeue files that were previously being processed but did not finish
                foreach (var file in Directory.EnumerateFiles(processingDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.Add(new IngestionFileDescriptor(userId, Path.GetFileName(file), file));
                }

                // Move fresh files from the root into the processing directory to reserve them
                foreach (var file in Directory.EnumerateFiles(userDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }

                    var destination = EnsureUniquePath(Path.Combine(processingDirectory, fileName));
                    try
                    {
                        File.Move(file, destination);
                        result.Add(new IngestionFileDescriptor(userId, Path.GetFileName(destination), destination));
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Failed to move file {File} into processing directory.", file);
                    }
                }
            }

            return Task.FromResult<IReadOnlyList<IngestionFileDescriptor>>(result);
        }

        public Task MarkProcessedAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var processedDirectory = EnsureDirectory(GetTargetDirectory(descriptor, _settings.ProcessedSubfolderName));
            var destination = EnsureUniquePath(Path.Combine(processedDirectory, descriptor.FileName));

            File.Move(descriptor.FullPath, destination, overwrite: false);
            _logger.LogInformation("Moved ingested file {File} to processed folder {Destination}.", descriptor.FullPath, destination);

            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(IngestionFileDescriptor descriptor, string? reason, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var failedDirectory = EnsureDirectory(GetTargetDirectory(descriptor, _settings.FailedSubfolderName));
            var destination = EnsureUniquePath(Path.Combine(failedDirectory, descriptor.FileName));

            File.Move(descriptor.FullPath, destination, overwrite: false);

            if (!string.IsNullOrWhiteSpace(reason))
            {
                var infoPath = destination + ".error.txt";
                File.WriteAllText(infoPath, reason, Encoding.UTF8);
            }

            _logger.LogWarning("Moved ingested file {File} to failed folder {Destination}.", descriptor.FullPath, destination);

            return Task.CompletedTask;
        }

        public async Task<string> SaveIncomingFileAsync(string userId, string fileName, Stream content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("UserId must be provided", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(_settings.RootPath))
            {
                throw new InvalidOperationException("Filesystem ingestion provider is not configured with a root path.");
            }

            var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? Guid.NewGuid().ToString("N") : fileName);
            var userDirectory = EnsureDirectory(Path.Combine(_settings.RootPath, userId));

            var destination = EnsureUniquePath(Path.Combine(userDirectory, safeFileName));

            await using (var fileStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
            {
                await content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Stored ingestion file for user {UserId} at {Destination}.", userId, destination);

            return destination;
        }

        public Task<byte[]> ReadFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return File.ReadAllBytesAsync(descriptor.FullPath, cancellationToken);
        }

        private string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }

        private string GetTargetDirectory(IngestionFileDescriptor descriptor, string subfolderName)
        {
            var processingDirectory = Path.GetDirectoryName(descriptor.FullPath);
            if (string.IsNullOrEmpty(processingDirectory))
            {
                throw new InvalidOperationException($"Cannot determine processing directory for {descriptor.FullPath}");
            }

            var userDirectory = Directory.GetParent(processingDirectory)?.FullName;
            if (string.IsNullOrEmpty(userDirectory))
            {
                throw new InvalidOperationException($"Cannot determine user directory for {descriptor.FullPath}");
            }

            return Path.Combine(userDirectory, subfolderName);
        }

        private static string EnsureUniquePath(string desiredPath)
        {
            if (!File.Exists(desiredPath))
            {
                return desiredPath;
            }

            var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(desiredPath);
            var extension = Path.GetExtension(desiredPath);

            var counter = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, $"{name}_{counter}{extension}");
                counter++;
            }
            while (File.Exists(candidate));

            return candidate;
        }
    }
}
