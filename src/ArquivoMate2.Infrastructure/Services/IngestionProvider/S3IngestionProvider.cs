using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.IngestionProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeTypes;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Minio.DataModel.Encryption;
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
        private readonly SseCustomerKey? _customerEncryptionKey;

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
            _customerEncryptionKey = _settings.CustomerEncryption?.CreateCustomerKey();
        }

        /// <summary>
        /// Optional configured sender email for ingestion created EmailDocuments.
        /// Returns null if not configured.
        /// </summary>
        public string? IngestionEmailAddress => string.IsNullOrWhiteSpace(_settings.IngestionEmail) ? null : _settings.IngestionEmail;

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

        public async Task MarkProcessedAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPrefix = BuildUserSubfolder(descriptor.UserId, _settings.ProcessedSubfolderName);
            var destinationKey = await EnsureUniqueObjectKeyAsync(destinationPrefix, descriptor.FileName, cancellationToken).ConfigureAwait(false);

            await CopyObjectAsync(descriptor.FullPath, destinationKey, cancellationToken).ConfigureAwait(false);
            await RemoveObjectAsync(descriptor.FullPath, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Moved ingested file {File} to processed prefix {Destination}.", descriptor.FullPath, destinationKey);
        }

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

            if (_customerEncryptionKey is not null)
            {
                putArgs = putArgs.WithServerSideEncryption(_customerEncryptionKey);
            }

            await _minioClient.PutObjectAsync(putArgs, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Stored ingestion file for user {UserId} at {Destination}.", userId, destinationKey);

            return destinationKey;
        }

        public async Task<byte[]> ReadFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var ms = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(descriptor.FullPath)
                .WithCallbackStream(stream => stream.CopyTo(ms));

            if (_customerEncryptionKey is not null)
            {
                args = args.WithServerSideEncryption(_customerEncryptionKey);
            }

            await _minioClient.GetObjectAsync(args, cancellationToken).ConfigureAwait(false);
            return ms.ToArray();
        }

        private async Task<string> MoveToProcessingAsync(string userId, string sourceKey, string fileName, CancellationToken cancellationToken)
        {
            var destinationPrefix = BuildUserSubfolder(userId, _settings.ProcessingSubfolderName);
            var destinationKey = await EnsureUniqueObjectKeyAsync(destinationPrefix, fileName, cancellationToken).ConfigureAwait(false);

            await CopyObjectAsync(sourceKey, destinationKey, cancellationToken).ConfigureAwait(false);
            await RemoveObjectAsync(sourceKey, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Reserved ingestion file {Source} at {Destination} for user {UserId}.", sourceKey, destinationKey, userId);
            return destinationKey;
        }

        private async Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken)
        {
            var source = new CopySourceObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(sourceKey);

            if (_customerEncryptionKey is not null)
            {
                source = source.WithServerSideEncryption(_customerEncryptionKey);
            }

            var args = new CopyObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(destinationKey)
                .WithCopyObjectSource(source);

            if (_customerEncryptionKey is not null)
            {
                args = args.WithServerSideEncryption(_customerEncryptionKey)
                           .WithCopySourceEncryption(_customerEncryptionKey);
            }

            await _minioClient.CopyObjectAsync(args, cancellationToken).ConfigureAwait(false);
        }

        private Task RemoveObjectAsync(string objectKey, CancellationToken cancellationToken)
        {
            var args = new RemoveObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectKey);

            return _minioClient.RemoveObjectAsync(args, cancellationToken);
        }

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

        private async Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken)
        {
            try
            {
                var args = new StatObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(objectKey);

                if (_customerEncryptionKey is not null)
                {
                    args = args.WithServerSideEncryption(_customerEncryptionKey);
                }

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

        private async Task WriteErrorInfoAsync(string objectKey, string reason, CancellationToken cancellationToken)
        {
            await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(reason));
            var args = new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectKey)
                .WithStreamData(ms)
                .WithObjectSize(ms.Length)
                .WithContentType("text/plain; charset=utf-8");

            if (_customerEncryptionKey is not null)
            {
                args = args.WithServerSideEncryption(_customerEncryptionKey);
            }

            await _minioClient.PutObjectAsync(args, cancellationToken).ConfigureAwait(false);
        }

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

            // Directly call the Minio client's typed IAsyncEnumerable-based API
            await foreach (var item in _minioClient.ListObjectsEnumAsync(args, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }

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

        private string BuildUserSubfolder(string userId, string? subfolder)
        {
            var segments = new List<string> { userId };
            if (!string.IsNullOrWhiteSpace(subfolder))
            {
                segments.Add(subfolder);
            }

            return string.Join('/', segments);
        }

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

        private string NormalizeRootPrefix() => _settings.RootPrefix?.Trim('/') ?? string.Empty;
    }
}
