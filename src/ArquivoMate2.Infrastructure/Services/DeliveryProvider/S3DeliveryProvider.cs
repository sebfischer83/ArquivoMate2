using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.DeliveryProvider;
using EasyCaching.Core;
using Microsoft.Extensions.Options;
using Minio.DataModel.Args;
using Minio;
using Minio.DataModel.Encryption;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.DeliveryProvider
{
    /// <summary>
    /// Provides presigned URLs for accessing documents stored in S3-compatible storage.
    /// </summary>
    public class S3DeliveryProvider : IDeliveryProvider
    {
        private readonly S3DeliveryProviderSettings _settings;
        private readonly IMinioClient _storage;
        private readonly IEasyCachingProvider _cache;
        private readonly SseCustomerKey? _customerEncryptionKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="S3DeliveryProvider"/> class.
        /// </summary>
        /// <param name="opts">Provider configuration options.</param>
        /// <param name="minioClientFactory">Factory for creating MinIO clients.</param>
        /// <param name="cachingProviderFactory">Factory for resolving cache providers.</param>
        public S3DeliveryProvider(
            IOptions<S3DeliveryProviderSettings> opts,
            IMinioClientFactory minioClientFactory,
            IEasyCachingProviderFactory cachingProviderFactory)
        {
            _settings = opts.Value;
            _storage = minioClientFactory.CreateClient();
            _customerEncryptionKey = _settings.CustomerEncryption?.CreateCustomerKey();
            if (_customerEncryptionKey is not null && _settings.IsPublic)
            {
                throw new InvalidOperationException("SSE-C customer encryption cannot be combined with public S3 delivery.");
            }
            _cache = cachingProviderFactory.GetCachingProvider(EasyCachingConstValue.DefaultRedisName);
        }

        /// <summary>
        /// Resolves a downloadable URL for the specified object path.
        /// </summary>
        /// <param name="fullPath">Path to the object in storage.</param>
        /// <returns>A presigned or direct URL to the object.</returns>
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
                // Direct URL for public objects
                var directUrl = $"https://{_settings.Endpoint}/{_settings.BucketName}/{fullPath}";
                await _cache.SetAsync(cacheKey, directUrl, TimeSpan.FromHours(24));
                return directUrl;
            }

            // Signed URL for private objects
            var args = new PresignedGetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(fullPath)
                .WithExpiry((int)TimeSpan.FromHours(1).TotalSeconds);

            if (_customerEncryptionKey is not null)
            {
                args = args.WithServerSideEncryption(_customerEncryptionKey);
            }

            var presignedUrl = await _storage.PresignedGetObjectAsync(args);
            await _cache.SetAsync(cacheKey, presignedUrl, TimeSpan.FromMinutes(55));

            return presignedUrl;
        }
    }
}
