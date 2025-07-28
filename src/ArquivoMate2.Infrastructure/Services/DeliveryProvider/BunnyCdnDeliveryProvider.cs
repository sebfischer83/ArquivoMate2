using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.DeliveryProvider;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using EasyCaching.Core;
using Microsoft.Extensions.Options;
using Minio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.DeliveryProvider
{
    public class BunnyCdnDeliveryProvider : IDeliveryProvider
    {
        private readonly BunnyDeliveryProviderSettings _settings;
        private readonly IEasyCachingProvider _cache;

        public BunnyCdnDeliveryProvider(
            IOptions<BunnyDeliveryProviderSettings> opts,
            IEasyCachingProviderFactory cachingProviderFactory)
        {
            _settings = opts.Value;
            _cache = cachingProviderFactory.GetCachingProvider(EasyCachingConstValue.DefaultRedisName);
        }

        public async Task<string> GetAccessUrl(string fullPath)
        {
            var cacheKey = $"bunnyDelivery:{fullPath}";
            var cachedUrl = await _cache.GetAsync<string>(cacheKey);

            if (cachedUrl.HasValue)
            {
                return cachedUrl.Value;
            }


            
        }
    }
}
