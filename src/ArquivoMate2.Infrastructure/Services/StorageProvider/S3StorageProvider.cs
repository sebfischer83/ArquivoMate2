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
using EasyCaching.Core;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.DataProtection;
using Minio;
using Minio.DataModel.Args;

namespace ArquivoMate2.Infrastructure.Services.StorageProvider
{
    public class S3StorageProvider : IStorageProvider
    {
        private readonly S3StorageProviderSettings   _settings;
        private readonly IMinioClient _storage;
        private readonly IMinioClientFactory _minioClientFactory;
        private readonly IEasyCachingProviderFactory _easyCachingProviderFactory;
        private readonly IEasyCachingProvider _cache;

        public S3StorageProvider(IOptions<S3StorageProviderSettings> opts, IMinioClientFactory minioClientFactory, IEasyCachingProviderFactory easyCachingProviderFactory)
        {
            _settings = opts.Value;
            _storage = minioClientFactory.CreateClient();
            _minioClientFactory = minioClientFactory;
            _easyCachingProviderFactory = easyCachingProviderFactory;
            _cache = easyCachingProviderFactory.GetCachingProvider(EasyCachingConstValue.DefaultRedisName);
        }

        public async Task<string> GetAccessUrl(string fullPath)
        {
            if (_settings.IsPublic)
            {

            }
            var cacheKey = $"s3url:{fullPath}";
            var cachedUrl = await _cache.GetAsync<string>(cacheKey);

            if (cachedUrl.HasValue)
            {
                return cachedUrl.Value;
            }

            var args = new PresignedGetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithExpiry((int)TimeSpan.FromHours(1).TotalMinutes);

            var presignedUrl = await _storage.PresignedGetObjectAsync(args);

            await _cache.SetAsync(cacheKey, presignedUrl, TimeSpan.FromMinutes(50));

            return presignedUrl;
        }

        public async Task<string> SaveFile(string userId, Guid documentId, string filename, byte[] file)
        {
            var mimeType = MimeTypeMap.GetMimeType(filename);
            using var stream = new MemoryStream(file);

            string fullPath = $"{userId}/{documentId}/{filename}";
            var putObjectArgs = new PutObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(fullPath)
                    .WithStreamData(stream)
                    .WithObjectSize(stream.Length)
                    .WithContentType(mimeType);
            await _storage.PutObjectAsync(putObjectArgs);

            return fullPath;
        }
    }
}
