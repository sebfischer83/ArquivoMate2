using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.DeliveryProvider;
using EasyCaching.Core;
using Microsoft.Extensions.Options;
using Minio.DataModel.Args;
using Minio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.DeliveryProvider
{
    public class S3DeliveryProvider : IDeliveryProvider
    {
        private readonly S3DeliveryProviderSettings _settings;
        private readonly IMinioClient _storage;
        private readonly IEasyCachingProvider _cache;

        public S3DeliveryProvider(
            IOptions<S3DeliveryProviderSettings> opts,
            IMinioClientFactory minioClientFactory,
            IEasyCachingProviderFactory cachingProviderFactory)
        {
            _settings = opts.Value;
            _storage = minioClientFactory.CreateClient();
            _cache = cachingProviderFactory.GetCachingProvider(EasyCachingConstValue.DefaultRedisName);
        }

        public async Task<string> GetAccessUrl(string fullPath)
        {
            var cacheKey = $"s3delivery:{fullPath}";
            var cachedUrl = await _cache.GetAsync<string>(cacheKey);

            if (cachedUrl.HasValue)
            {
                return cachedUrl.Value;
            }

            if (_settings.IsPublic)
            {
                // Direkte URL für öffentliche Objekte
                var directUrl = $"https://{_settings.Endpoint}/{_settings.BucketName}/{fullPath}";
                await _cache.SetAsync(cacheKey, directUrl, TimeSpan.FromHours(24));
                return directUrl;
            }

            // Signierte URL für private Objekte
            var args = new PresignedGetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithExpiry((int)TimeSpan.FromHours(1).TotalMinutes);

            var presignedUrl = await _storage.PresignedGetObjectAsync(args);
            await _cache.SetAsync(cacheKey, presignedUrl, TimeSpan.FromMinutes(55));

            return presignedUrl;
        }
    }
}
