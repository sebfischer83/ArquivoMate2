using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using MimeTypes;
using FluentStorage.Blobs;
using FluentStorage;
using FluentStorage.AWS.Blobs;

namespace ArquivoMate2.Infrastructure.Services.StorageProvider
{
    public class S3StorageProvider : IStorageProvider
    {
        private readonly S3StorageProviderSettings   _settings;
        private readonly IAwsS3BlobStorage _storage;

        public S3StorageProvider(IOptions<S3StorageProviderSettings> opts, IAwsS3BlobStorage storage)
        {
            _settings = opts.Value;
            _storage = storage;

            //_storage = (IAwsS3BlobStorage) StorageFactory.Blobs.FromConnectionString();
        }

        public async Task SaveFile(string userId, Guid documentId, string filename, byte[] file)
        {
            var mimeType = MimeTypeMap.GetMimeType(filename);
            using var stream = new MemoryStream(file);

            await _storage.WriteAsync($"{userId}/{documentId}/{filename}", file);
        }
    }
}
