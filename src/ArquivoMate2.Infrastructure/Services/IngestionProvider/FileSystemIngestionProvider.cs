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

        /// <summary>
        /// Initializes a new instance of <see cref="FileSystemIngestionProvider"/> using the provided settings and logger.
        /// </summary>
        public FileSystemIngestionProvider(
            IOptions<FileSystemIngestionProviderSettings> options,
            ILogger<FileSystemIngestionProvider> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Discovers and reserves pending ingestion files across user directories by moving fresh files into each user's processing subfolder.
        /// </summary>
        /// <remarks>
        /// If the configured root path is missing, it is created. Files already present in a user's processing subfolder are returned as requeued items; top-level files in a user's directory are moved into the processing subfolder to reserve them. Failed file moves are logged and do not stop processing of other files.
        /// </remarks>
        /// <returns>An IReadOnlyList of IngestionFileDescriptor entries representing files reserved for processing or requeued from processing; returns an empty list if the root path is not configured or no files are found.</returns>
        /// <exception cref="OperationCanceledException">If cancellation is requested via the provided <see cref="CancellationToken"/>.</exception>
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

        /// <summary>
        /// Move the file represented by <paramref name="descriptor"/> into the provider's processed subfolder for its user, ensuring the destination path is unique.
        /// </summary>
        /// <param name="descriptor">Descriptor identifying the file to mark as processed (provides the file's current path and name).</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        public Task MarkProcessedAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var processedDirectory = EnsureDirectory(GetTargetDirectory(descriptor, _settings.ProcessedSubfolderName));
            var destination = EnsureUniquePath(Path.Combine(processedDirectory, descriptor.FileName));

            File.Move(descriptor.FullPath, destination, overwrite: false);
            _logger.LogInformation("Moved ingested file {File} to processed folder {Destination}.", descriptor.FullPath, destination);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Moves the file identified by <paramref name="descriptor"/> into the configured failed subfolder and records an optional failure reason.
        /// </summary>
        /// <param name="descriptor">The ingestion file descriptor whose file will be moved to the failed folder.</param>
        /// <param name="reason">Optional human-readable failure reason; when non-empty, it is written to a sibling `.error.txt` file next to the moved file.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
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

        /// <summary>
        /// Saves an incoming file stream into the configured user's directory and returns the stored file path.
        /// </summary>
        /// <param name="userId">The identifier of the user who owns the file; must not be null or empty.</param>
        /// <param name="fileName">The desired file name; if null or empty a GUID-based name is generated. Path components are stripped.</param>
        /// <param name="content">The stream containing the file contents to be written.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The full path to the stored file.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the provider's root path is not configured.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via <paramref name="cancellationToken"/>.</exception>
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

        /// <summary>
        /// Read the contents of the file referenced by the given ingestion descriptor.
        /// </summary>
        /// <param name="descriptor">Descriptor whose FullPath points to the file to read.</param>
        /// <param name="cancellationToken">Token to cancel the read operation.</param>
        /// <returns>The file contents as a byte array.</returns>
        /// <exception cref="OperationCanceledException">If <paramref name="cancellationToken"/> is canceled before or during the read.</exception>
        public Task<byte[]> ReadFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return File.ReadAllBytesAsync(descriptor.FullPath, cancellationToken);
        }

        /// <summary>
        /// Ensures a directory exists at the specified path, creating it if necessary.
        /// </summary>
        /// <param name="path">The directory path to ensure exists.</param>
        /// <returns>The same directory path passed in <paramref name="path"/>.</returns>
        private string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// Determines the full path to a named subfolder under the user directory for the given ingestion file descriptor.
        /// </summary>
        /// <param name="descriptor">The ingestion file descriptor whose location is used to resolve the user directory.</param>
        /// <param name="subfolderName">The name of the target subfolder (for example, "processing", "processed", or "failed").</param>
        /// <returns>The full path to the specified subfolder within the descriptor's user directory.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the descriptor's directory or its parent user directory cannot be determined.</exception>
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

        /// <summary>
        /// Finds a non-conflicting file path based on the provided desired path.
        /// </summary>
        /// <param name="desiredPath">The initial desired file path (including directory, name, and extension).</param>
        /// <returns>
        /// The original path if no file exists at that location; otherwise a variant with an appended `_1`, `_2`, etc. suffix before the extension that does not exist.
        /// </returns>
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