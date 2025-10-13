using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using Microsoft.Extensions.Options;
using MimeTypes;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Encryption;
using Minio.Exceptions;
using Polly;
using Polly.Contrib.WaitAndRetry;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.StorageProvider
{
    public class S3StorageProvider : StorageProviderBase<S3StorageProviderSettings>
    {
        private readonly IMinioClient _storage;
        private AsyncPolicy _minioRetryPolicy;
        private readonly SSEC? _ssec;

        public S3StorageProvider(IOptions<S3StorageProviderSettings> opts, IMinioClientFactory minioClientFactory, IPathService pathService)
            : base(opts, pathService)
        {
            _storage = minioClientFactory.CreateClient();

            // Validate SSE-C configuration if enabled
            _settings.SseC?.Validate();

            // Create SSEC object if enabled
            if (_settings.SseC?.Enabled == true)
            {
                var key = Convert.FromBase64String(_settings.SseC.CustomerKeyBase64);
                _ssec = new SSEC(key);
            }

            // Initialize Polly retry policy for MinIO operations
            _minioRetryPolicy = Policy
                .Handle<MinioException>()
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(200), 5));
        }

        public override async Task<string> SaveFile(string userId, Guid documentId, string filename, byte[] file, string artifact = "file")
        {
            using var stream = new MemoryStream(file, writable: false);
            return await SaveFileAsync(userId, documentId, filename, stream, artifact).ConfigureAwait(false);
        }

        public override async Task<string> SaveFileAsync(string userId, Guid documentId, string filename, Stream content, string artifact = "file", CancellationToken ct = default)
        {
            var mimeType = MimeTypeMap.GetMimeType(filename);
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            string fullPath = BuildObjectPath(userId, documentId, filename);
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithStreamData(content)
                .WithContentType(mimeType);

            if (content.CanSeek)
            {
                putObjectArgs = putObjectArgs.WithObjectSize(content.Length);
            }
            else
            {
                putObjectArgs = putObjectArgs.WithObjectSize(-1);
            }

            // Apply SSE-C if enabled
            if (_ssec != null)
            {
                putObjectArgs = putObjectArgs.WithServerSideEncryption(_ssec);
            }

            // MINIO_RETRY: wrapped
            await RunWithMinioRetry(ct => _storage.PutObjectAsync(putObjectArgs, ct), ct).ConfigureAwait(false);
            return fullPath;
        }

        public override async Task<byte[]> GetFileAsync(string fullPath, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithCallbackStream(stream => stream.CopyTo(ms));

            // Apply SSE-C if enabled
            if (_ssec != null)
            {
                args = args.WithServerSideEncryption(_ssec);
            }

            // MINIO_RETRY: wrapped
            await RunWithMinioRetry(ct => _storage.GetObjectAsync(args, ct), ct).ConfigureAwait(false);
            return ms.ToArray();
        }

        public override async Task StreamFileAsync(string fullPath, Func<Stream, CancellationToken, Task> streamConsumer, CancellationToken ct = default)
        {
            if (streamConsumer == null) throw new ArgumentNullException(nameof(streamConsumer));

            using var ms = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithCallbackStream(stream =>
                {
                    // Buffer the stream synchronously into the memory stream
                    stream.CopyTo(ms);
                });

            // Apply SSE-C if enabled
            if (_ssec != null)
            {
                args = args.WithServerSideEncryption(_ssec);
            }

            // MINIO_RETRY: wrapped
            await RunWithMinioRetry(ct => _storage.GetObjectAsync(args, ct), ct).ConfigureAwait(false);
            ms.Position = 0;
            await streamConsumer(ms, ct).ConfigureAwait(false);
        }

        // MINIO retry helpers
        private Task RunWithMinioRetry(Func<Task> action)
            => _minioRetryPolicy.ExecuteAsync(action);

        private Task RunWithMinioRetry(Func<CancellationToken, Task> action, CancellationToken ct)
            => _minioRetryPolicy.ExecuteAsync(ct2 => action(ct2), ct);

        private Task<T> RunWithMinioRetry<T>(Func<Task<T>> action)
        {
            // Create a generic policy using the same backoff strategy to support returning values.
            var backoff = Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(200), 5);
            var policy = Policy.Handle<MinioException>()
                               .Or<HttpRequestException>()
                               .WaitAndRetryAsync(backoff);

            return policy.ExecuteAsync(action);
        }

        private Task<T> RunWithMinioRetry<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            var backoff = Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(200), 5);
            var policy = Policy.Handle<MinioException>()
                               .Or<HttpRequestException>()
                               .WaitAndRetryAsync(backoff);

            return policy.ExecuteAsync(ct2 => action(ct2), ct);
        }
    }
}
