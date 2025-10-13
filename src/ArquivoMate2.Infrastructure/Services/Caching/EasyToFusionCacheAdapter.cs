using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace ArquivoMate2.Infrastructure.Services.Caching
{
    /// <summary>
    /// Adapter that exposes an EasyCaching-like API backed by FusionCache while instrumenting operations with OpenTelemetry.
    /// </summary>
    public sealed class EasyToFusionCacheAdapter : IAppCache
    {
        private static readonly ActivitySource ActivitySource = new("App.Caching");

        private readonly IFusionCache _cache;
        private readonly string _prefix;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly ITtlResolver _ttlResolver;
        private readonly ILogger<EasyToFusionCacheAdapter> _logger;

        public EasyToFusionCacheAdapter(
            IFusionCache cache,
            IOptions<CachingOptions> options,
            ITtlResolver ttlResolver,
            ILogger<EasyToFusionCacheAdapter> logger)
        {
            _cache = cache;
            _prefix = options.Value.KeyPrefix ?? string.Empty;
            _ttlResolver = ttlResolver;
            _logger = logger;
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        private string PrefixKey(string key) => string.Concat(_prefix, key);

        private int EstimateSize<T>(string key, T value)
        {
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(value, _serializerOptions));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to estimate cache entry size for key {CacheKey}", key);
                return 0;
            }
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            var fullKey = PrefixKey(key);
            using var activity = ActivitySource.StartActivity("cache.get");
            activity?.SetTag("cache.key", fullKey);

            var resolved = _ttlResolver.Resolve(key);
            var duration = resolved.Ttl;
            var useSliding = resolved.Sliding;

            var cached = await _cache.TryGetAsync<T>(fullKey).ConfigureAwait(false);
            var hasValue = cached.HasValue;
            activity?.SetTag("cache.hit", hasValue);
            activity?.SetTag("cache.ttl.ms", duration.TotalMilliseconds);
            activity?.SetTag("cache.slidingRequested", useSliding);
            // We support sliding semantics by refreshing the TTL on access (re-setting the entry)
            activity?.SetTag("cache.slidingSupported", true);

            if (hasValue)
            {
                if (useSliding)
                {
                    try
                    {
                        var estimatedSize = EstimateSize(fullKey, cached.Value);
                        var options = new FusionCacheEntryOptions { Duration = duration, Size = estimatedSize };
                        // Refresh TTL by re-writing the entry (best-effort)
                        await _cache.SetAsync(fullKey, cached.Value, options).ConfigureAwait(false);
                        activity?.SetTag("cache.refreshed", true);
                    }
                    catch (Exception ex)
                    {
                        // don't fail on refresh; just log
                        _logger.LogDebug(ex, "Failed to refresh sliding TTL for cache key {CacheKey}", fullKey);
                        activity?.SetTag("cache.refreshed", false);
                    }
                }

                return cached.Value;
            }

            return default;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, bool? sliding = null, CancellationToken ct = default)
        {
            var fullKey = PrefixKey(key);
            var resolved = _ttlResolver.Resolve(key);
            var duration = ttl ?? resolved.Ttl;
            var useSliding = sliding ?? resolved.Sliding;
            var estimatedSize = EstimateSize(fullKey, value);

            using var activity = ActivitySource.StartActivity("cache.set");
            activity?.SetTag("cache.key", fullKey);
            activity?.SetTag("cache.ttl.ms", duration.TotalMilliseconds);
            activity?.SetTag("cache.slidingRequested", useSliding);
            // Sliding is implemented via TTL refresh on access
            activity?.SetTag("cache.slidingSupported", true);
            activity?.SetTag("cache.size.bytes", estimatedSize);

            var options = new FusionCacheEntryOptions
            {
                Duration = duration,
                Size = estimatedSize
            };

            await _cache.SetAsync(fullKey, value, options).ConfigureAwait(false);
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, bool? sliding = null, CancellationToken ct = default)
        {
            var fullKey = PrefixKey(key);
            var resolved = _ttlResolver.Resolve(key);
            var duration = ttl ?? resolved.Ttl;
            var useSliding = sliding ?? resolved.Sliding;

            using var activity = ActivitySource.StartActivity("cache.getorset");
            activity?.SetTag("cache.key", fullKey);
            activity?.SetTag("cache.ttl.ms", duration.TotalMilliseconds);
            activity?.SetTag("cache.slidingRequested", useSliding);
            activity?.SetTag("cache.slidingSupported", true);

            var existing = await _cache.TryGetAsync<T>(fullKey).ConfigureAwait(false);
            if (existing.HasValue)
            {
                activity?.SetTag("cache.hit", true);

                if (useSliding)
                {
                    try
                    {
                        var estimatedSize = EstimateSize(fullKey, existing.Value);
                        var options = new FusionCacheEntryOptions { Duration = duration, Size = estimatedSize };
                        await _cache.SetAsync(fullKey, existing.Value, options).ConfigureAwait(false);
                        activity?.SetTag("cache.refreshed", true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to refresh sliding TTL for cache key {CacheKey}", fullKey);
                        activity?.SetTag("cache.refreshed", false);
                    }
                }

                return existing.Value;
            }

            activity?.SetTag("cache.hit", false);

            // Fall back to manual population to avoid problematic generic overload resolution on IFusionCache.GetOrSetAsync
            var value = await factory(ct).ConfigureAwait(false);
            var estimatedSizeNew = EstimateSize(fullKey, value);

            var optionsNew = new FusionCacheEntryOptions
            {
                Duration = duration,
                Size = estimatedSizeNew
            };

            activity?.SetTag("cache.size.bytes", estimatedSizeNew);

            await _cache.SetAsync(fullKey, value, optionsNew).ConfigureAwait(false);

            return value;
        }

        public async Task RemoveAsync(string key, CancellationToken ct = default)
        {
            var fullKey = PrefixKey(key);
            using var activity = ActivitySource.StartActivity("cache.remove");
            activity?.SetTag("cache.key", fullKey);
            await _cache.RemoveAsync(fullKey).ConfigureAwait(false);
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        {
            var fullKey = PrefixKey(key);
            var cached = await _cache.TryGetAsync<object>(fullKey).ConfigureAwait(false);
            return cached.HasValue;
        }
    }
}
