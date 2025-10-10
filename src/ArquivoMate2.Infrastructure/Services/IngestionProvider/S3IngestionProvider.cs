using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.IngestionProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeTypes;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.IngestionProvider
{
    /// <summary>
    /// S3-compatible ingestion provider that organizes pending files in user specific prefixes
    /// and manages their lifecycle during processing.
    /// </summary>
    public class S3IngestionProvider : IIngestionProvider
    {
        private readonly S3IngestionProviderSettings _settings;
        private readonly ILogger<S3IngestionProvider> _logger;
        private readonly IMinioClient _minioClient;

        /// <summary>
        /// Initializes a new instance of <see cref="S3IngestionProvider"/> and configures the MinIO client used for S3-compatible storage operations.
        /// </summary>
        /// <remarks>
        /// Stores the provided settings and logger, and builds a MinIO client using the configured endpoint and credentials; applies the configured region and SSL option when provided.
        /// </remarks>
        public S3IngestionProvider(IOptions<S3IngestionProviderSettings> options, ILogger<S3IngestionProvider> logger)
        {
            _settings = options.Value;
            _logger = logger;

            var clientBuilder = new MinioClient()
                .WithEndpoint(_settings.Endpoint)
                .WithCredentials(_settings.AccessKey, _settings.SecretKey);

            if (!string.IsNullOrWhiteSpace(_settings.Region))
            {
                clientBuilder = clientBuilder.WithRegion(_settings.Region);
            }

            if (_settings.UseSsl)
            {
                clientBuilder = clientBuilder.WithSSL();
            }

            _minioClient = clientBuilder.Build();
        }

        /// <summary>
        /// Scans the bucket for user-scoped ingestion files, reserves unprocessed files by moving them into the processing subfolder, and returns descriptors for files that are reserved or already in the processing subfolder.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A read-only list of <see cref="IngestionFileDescriptor"/> instances representing files reserved for processing or already inside the processing subfolder.</returns>
        public async Task<IReadOnlyList<IngestionFileDescriptor>> ListPendingFilesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var descriptors = new List<IngestionFileDescriptor>();

            await foreach (var item in ListObjectsAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.IsDir)
                {
                    continue;
                }

                var key = item.Key;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var relativeKey = GetRelativeKey(key);
                if (string.IsNullOrEmpty(relativeKey))
                {
                    continue;
                }

                var segments = relativeKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 2)
                {
                    continue;
                }

                var userId = segments[0];
                if (string.IsNullOrWhiteSpace(userId))
                {
                    continue;
                }

                // Already reserved file inside the processing folder.
                if (segments.Length >= 3 && segments[1].Equals(_settings.ProcessingSubfolderName, StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = segments[^1];
                    descriptors.Add(new IngestionFileDescriptor(userId, fileName, key));
                    continue;
                }

                // Ignore files that have already been processed or failed.
                if (segments.Length >= 3 &&
                    (segments[1].Equals(_settings.ProcessedSubfolderName, StringComparison.OrdinalIgnoreCase)
                        || segments[1].Equals(_settings.FailedSubfolderName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (segments.Length == 2)
                {
                    var fileName = segments[1];
                    var reservedKey = await MoveToProcessingAsync(userId, key, fileName, cancellationToken).ConfigureAwait(false);
                    descriptors.Add(new IngestionFileDescriptor(userId, Path.GetFileName(reservedKey), reservedKey));
                }
            }

            return descriptors;
        }

        /// <summary>
        /// Moves the specified ingestion file into the user's processed subfolder and assigns a unique destination key.
        /// </summary>
        /// <param name="descriptor">Descriptor identifying the file to mark as processed, including the user ID, file name, and source path.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        public async Task MarkProcessedAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPrefix = BuildUserSubfolder(descriptor.UserId, _settings.ProcessedSubfolderName);
            var destinationKey = await EnsureUniqueObjectKeyAsync(destinationPrefix, descriptor.FileName, cancellationToken).ConfigureAwait(false);

            await CopyObjectAsync(descriptor.FullPath, destinationKey, cancellationToken).ConfigureAwait(false);
            await RemoveObjectAsync(descriptor.FullPath, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Moved ingested file {File} to processed prefix {Destination}.", descriptor.FullPath, destinationKey);
        }

        /// <summary>
        /// Moves the specified ingestion file into the user's failed subfolder and records an optional error reason.
        /// </summary>
        /// <param name="descriptor">Descriptor of the file to mark as failed; its user ID and file name determine the destination.</param>
        /// <param name="reason">Optional failure reason to store alongside the failed file; ignored if null or whitespace.</param>
        /// <param name="cancellationToken">Token that can be used to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via <paramref name="cancellationToken"/>.</exception>
        public async Task MarkFailedAsync(IngestionFileDescriptor descriptor, string? reason, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPrefix = BuildUserSubfolder(descriptor.UserId, _settings.FailedSubfolderName);
            var destinationKey = await EnsureUniqueObjectKeyAsync(destinationPrefix, descriptor.FileName, cancellationToken).ConfigureAwait(false);

            await CopyObjectAsync(descriptor.FullPath, destinationKey, cancellationToken).ConfigureAwait(false);
            await RemoveObjectAsync(descriptor.FullPath, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(reason))
            {
                await WriteErrorInfoAsync(destinationKey + ".error.txt", reason, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogWarning("Moved ingested file {File} to failed prefix {Destination}.", descriptor.FullPath, destinationKey);
        }

        /// <summary>
        /// Stores an incoming file under the specified user's root prefix and returns the stored object's key.
        /// </summary>
        /// <param name="userId">The identifier of the owning user; must be provided.</param>
        /// <param name="fileName">The original file name; if null or whitespace a safe name is generated and the final name is sanitized.</param>
        /// <param name="content">A stream containing the file data. If the stream is seekable, its position will be reset to 0 before upload.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The object key where the file was stored.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is null, empty, or consists only of whitespace.</exception>
        public async Task<string> SaveIncomingFileAsync(string userId, string fileName, Stream content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("UserId must be provided", nameof(userId));
            }

            var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? Guid.NewGuid().ToString("N") : fileName);
            var destinationPrefix = BuildUserSubfolder(userId, subfolder: null);
            var destinationKey = await EnsureUniqueObjectKeyAsync(destinationPrefix, safeFileName, cancellationToken).ConfigureAwait(false);

            if (content.CanSeek)
            {
                content.Position = 0;
            }

            var mimeType = MimeTypeMap.GetMimeType(safeFileName);
            var putArgs = new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(destinationKey)
                .WithStreamData(content)
                .WithObjectSize(content.CanSeek ? content.Length : -1)
                .WithContentType(mimeType);

            await _minioClient.PutObjectAsync(putArgs, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Stored ingestion file for user {UserId} at {Destination}.", userId, destinationKey);

            return destinationKey;
        }

        /// <summary>
        /// Reads the contents of the specified ingestion file and returns its bytes.
        /// </summary>
        /// <param name="descriptor">Descriptor identifying the file to read (uses the descriptor's FullPath).</param>
        /// <returns>The file's contents as a byte array.</returns>
        public async Task<byte[]> ReadFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var ms = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(descriptor.FullPath)
                .WithCallbackStream(stream => stream.CopyTo(ms));

            await _minioClient.GetObjectAsync(args, cancellationToken).ConfigureAwait(false);
            return ms.ToArray();
        }

        /// <summary>
        /// Reserves a user's file for processing by moving it into the user's processing subfolder.
        /// </summary>
        /// <param name="userId">Identifier of the owner of the file.</param>
        /// <param name="sourceKey">Current object key of the file to reserve.</param>
        /// <param name="fileName">Desired file name to use within the processing subfolder.</param>
        /// <returns>The destination object key where the file was moved.</returns>
        private async Task<string> MoveToProcessingAsync(string userId, string sourceKey, string fileName, CancellationToken cancellationToken)
        {
            var destinationPrefix = BuildUserSubfolder(userId, _settings.ProcessingSubfolderName);
            var destinationKey = await EnsureUniqueObjectKeyAsync(destinationPrefix, fileName, cancellationToken).ConfigureAwait(false);

            await CopyObjectAsync(sourceKey, destinationKey, cancellationToken).ConfigureAwait(false);
            await RemoveObjectAsync(sourceKey, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Reserved ingestion file {Source} at {Destination} for user {UserId}.", sourceKey, destinationKey, userId);
            return destinationKey;
        }

        /// <summary>
        /// Copies an object within the configured bucket from the specified source key to the specified destination key.
        /// </summary>
        /// <param name="sourceKey">The object key (path) of the source within the bucket.</param>
        /// <param name="destinationKey">The object key (path) to create or overwrite within the bucket.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
        private async Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken)
        {
            var source = new CopySourceObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(sourceKey);

            var args = new CopyObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(destinationKey)
                .WithCopyObjectSource(source);

            await _minioClient.CopyObjectAsync(args, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes the specified object from the configured bucket.
        /// </summary>
        /// <param name="objectKey">The object key within the bucket to remove.</param>
        /// <param name="cancellationToken">A token to cancel the removal operation.</param>
        private Task RemoveObjectAsync(string objectKey, CancellationToken cancellationToken)
        {
            var args = new RemoveObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectKey);

            return _minioClient.RemoveObjectAsync(args, cancellationToken);
        }

        /// <summary>
        /// Finds a non-colliding object key under the specified prefix by returning the original key if available or appending an incremental numeric suffix to the file name until an unused key is found.
        /// </summary>
        /// <param name="prefix">Optional prefix (user or root path) to prepend to the file name.</param>
        /// <param name="fileName">The desired file name, including extension if any.</param>
        /// <param name="cancellationToken">Token to observe for cancellation of the existence checks.</param>
        /// <returns>The chosen object key (prefix + file name) that does not exist in the bucket.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled via the provided token.</exception>
        private async Task<string> EnsureUniqueObjectKeyAsync(string prefix, string fileName, CancellationToken cancellationToken)
        {
            var baseKey = CombineSegments(prefix, fileName);
            if (!await ObjectExistsAsync(baseKey, cancellationToken).ConfigureAwait(false))
            {
                return baseKey;
            }

            var name = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var counter = 1;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = CombineSegments(prefix, $"{name}_{counter}{extension}");
                if (!await ObjectExistsAsync(candidate, cancellationToken).ConfigureAwait(false))
                {
                    return candidate;
                }

                counter++;
            }
        }

        /// <summary>
        /// Checks whether an object with the given key exists in the configured bucket.
        /// </summary>
        /// <param name="objectKey">The object key to check for existence within the bucket.</param>
        /// <returns>`true` if the object exists, `false` otherwise.</returns>
        private async Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken)
        {
            try
            {
                var args = new StatObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(objectKey);

                await _minioClient.StatObjectAsync(args, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (ObjectNotFoundException)
            {
                return false;
            }
            catch (MinioException ex) when (ex.Message.Contains("Not found", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        /// <summary>
        /// Writes a UTF-8 plain text object containing the provided error reason to the specified object key in the configured bucket.
        /// </summary>
        /// <param name="objectKey">Destination object key where the error information will be stored.</param>
        /// <param name="reason">Text describing the error; stored as UTF-8 plain text.</param>
        /// <param name="cancellationToken">Token to cancel the upload operation.</param>
        private async Task WriteErrorInfoAsync(string objectKey, string reason, CancellationToken cancellationToken)
        {
            await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(reason));
            var args = new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectKey)
                .WithStreamData(ms)
                .WithObjectSize(ms.Length)
                .WithContentType("text/plain; charset=utf-8");

            await _minioClient.PutObjectAsync(args, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerates objects in the configured bucket (recursively) under the provider's root prefix and yields Minio list items as they are discovered.
        /// </summary>
        /// <param name="cancellationToken">Token that can be used to cancel the asynchronous enumeration.</param>
        /// <returns>An asynchronous sequence of <c>Item</c> objects representing objects in the bucket.</returns>
        private async IAsyncEnumerable<Item> ListObjectsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var args = new ListObjectsArgs()
                .WithBucket(_settings.BucketName)
                .WithRecursive(true);

            var rootPrefix = NormalizeRootPrefix();
            if (!string.IsNullOrEmpty(rootPrefix))
            {
                args = args.WithPrefix(rootPrefix + "/");
            }

            var observable = _minioClient.ListObjectsAsync(args);
            var queue = new ConcurrentQueue<Item>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            using var subscription = observable.Subscribe(
                item => queue.Enqueue(item),
                ex => tcs.TrySetException(ex),
                () => tcs.TrySetResult(true));

            while (!queue.IsEmpty || !tcs.Task.IsCompleted)
            {
                while (queue.TryDequeue(out var queuedItem))
                {
                    yield return queuedItem;
                }

                if (!tcs.Task.IsCompleted)
                {
                    await Task.WhenAny(tcs.Task, Task.Delay(50, cancellationToken)).ConfigureAwait(false);
                }
            }

            // Ensure completion or propagate exception/cancellation.
            await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Builds a normalized object key by concatenating the configured root prefix, an optional prefix, and the file name, separated by '/'.
        /// </summary>
        /// <param name="prefix">An optional path segment under the root prefix (leading and trailing slashes are ignored).</param>
        /// <param name="fileName">The file name or terminal path segment to append.</param>
        /// <returns>The combined object key with empty segments removed and segments joined by '/'.</returns>
        private string CombineSegments(string? prefix, string fileName)
        {
            var segments = new List<string>();

            var rootPrefix = NormalizeRootPrefix();
            if (!string.IsNullOrEmpty(rootPrefix))
            {
                segments.Add(rootPrefix);
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                segments.Add(prefix.Trim('/'));
            }

            segments.Add(fileName);
            return string.Join('/', segments.Where(s => !string.IsNullOrEmpty(s)));
        }

        /// <summary>
        /// Builds a user-scoped object key prefix by combining the user ID with an optional subfolder.
        /// </summary>
        /// <param name="userId">The user identifier to use as the first path segment.</param>
        /// <param name="subfolder">An optional subfolder name to append as the second segment; ignored if null, empty, or whitespace.</param>
        /// <returns>The combined path segments joined by '/' (e.g. "userId" or "userId/subfolder").</returns>
        private string BuildUserSubfolder(string userId, string? subfolder)
        {
            var segments = new List<string> { userId };
            if (!string.IsNullOrWhiteSpace(subfolder))
            {
                segments.Add(subfolder);
            }

            return string.Join('/', segments);
        }

        /// <summary>
        /// Computes the object key relative to the configured root prefix.
        /// </summary>
        /// <param name="objectKey">The full object key as stored in the bucket.</param>
        /// <returns>The object key with the configured root prefix removed. If no root prefix is configured or the key does not start with the prefix, returns the original <paramref name="objectKey"/>. Removes a leading '/' from the result if present.</returns>
        private string GetRelativeKey(string objectKey)
        {
            var rootPrefix = NormalizeRootPrefix();
            if (string.IsNullOrEmpty(rootPrefix))
            {
                return objectKey;
            }

            if (!objectKey.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                return objectKey;
            }

            var relative = objectKey[rootPrefix.Length..];
            if (relative.StartsWith('/'))
            {
                relative = relative[1..];
            }

            return relative;
        }

        /// <summary>
/// Produce the configured root prefix with any leading or trailing slashes removed, or an empty string if no prefix is configured.
/// </summary>
/// <returns>The normalized root prefix without leading or trailing slashes, or an empty string when RootPrefix is null.</returns>
private string NormalizeRootPrefix() => _settings.RootPrefix?.Trim('/') ?? string.Empty;
    }
}