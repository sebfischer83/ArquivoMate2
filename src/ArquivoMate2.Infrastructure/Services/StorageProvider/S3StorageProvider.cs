using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using Microsoft.Extensions.Options;
using MimeTypes;
using EasyCaching.Core;
using Minio;
using Minio.DataModel.Args;

namespace ArquivoMate2.Infrastructure.Services.StorageProvider
{
    public class S3StorageProvider : IStorageProvider
    {
        private readonly S3StorageProviderSettings _settings;
        private readonly IMinioClient _storage;
        private readonly IPathService _pathService;

        public S3StorageProvider(IOptions<S3StorageProviderSettings> opts, IMinioClientFactory minioClientFactory, IEasyCachingProviderFactory easyCachingProviderFactory, IPathService pathService)
        {
            _settings = opts.Value;
            _storage = minioClientFactory.CreateClient();
            _pathService = pathService;
        }

        public async Task<string> SaveFile(string userId, Guid documentId, string filename, byte[] file, string artifact = "file")
        {
            var mimeType = MimeTypeMap.GetMimeType(filename);
            using var stream = new MemoryStream(file);
            string fullPath = "arquivomate/" + string.Join('/', _pathService.GetStoragePath(userId, documentId, filename));
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(mimeType);
            await _storage.PutObjectAsync(putObjectArgs);
            return fullPath;
        }

        public async Task<byte[]> GetFileAsync(string fullPath, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithCallbackStream(stream => stream.CopyTo(ms));
            await _storage.GetObjectAsync(args, ct);
            return ms.ToArray();
        }
    }
}
