using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using EasyCaching.Core;
using Microsoft.Extensions.Options;
using MimeTypes;
using Minio;
using Minio.DataModel.Args;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.StorageProvider
{
    public class S3StorageProvider : StorageProviderBase<S3StorageProviderSettings>
    {
        private readonly IMinioClient _storage;

        public S3StorageProvider(IOptions<S3StorageProviderSettings> opts, IMinioClientFactory minioClientFactory, IEasyCachingProviderFactory easyCachingProviderFactory, IPathService pathService)
            : base(opts, pathService)
        {
            _storage = minioClientFactory.CreateClient();
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
                putObjectArgs = putObjectArgs.WithObjectSize(-1).WithPartSize(5 * 1024 * 1024);
            }

            await _storage.PutObjectAsync(putObjectArgs, ct).ConfigureAwait(false);
            return fullPath;
        }

        public override async Task<byte[]> GetFileAsync(string fullPath, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithCallbackStream(stream => stream.CopyTo(ms));
            await _storage.GetObjectAsync(args, ct).ConfigureAwait(false);
            return ms.ToArray();
        }

        public override async Task StreamFileAsync(string fullPath, Func<Stream, CancellationToken, Task> streamConsumer, CancellationToken ct = default)
        {
            if (streamConsumer == null) throw new ArgumentNullException(nameof(streamConsumer));

            Exception? callbackException = null;
            var args = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithCallbackStream(stream =>
                {
                    try
                    {
                        streamConsumer(stream, ct).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        callbackException = ex;
                        throw;
                    }
                });

            try
            {
                await _storage.GetObjectAsync(args, ct).ConfigureAwait(false);
            }
            catch when (callbackException != null)
            {
                throw callbackException;
            }
        }
    }
}
