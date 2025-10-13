using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Abstraction for the application cache used throughout the solution.
    /// Provides asynchronous helpers for interacting with the multi-layer cache implementation.
    /// </summary>
    public interface IAppCache
    {
        /// <summary>
        /// Attempts to retrieve a cached value for the given key.
        /// </summary>
        /// <typeparam name="T">Type of the cached payload.</typeparam>
        /// <param name="key">Cache key without any provider-specific prefix.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The cached value or <c>null</c> when not present.</returns>
        Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

        /// <summary>
        /// Stores a value in the cache using the optional TTL overrides when provided.
        /// </summary>
        /// <typeparam name="T">Type of the value to cache.</typeparam>
        /// <param name="key">Cache key without any provider-specific prefix.</param>
        /// <param name="value">Value to store in the cache.</param>
        /// <param name="ttl">Optional explicit duration. When <c>null</c> the configured resolver is used.</param>
        /// <param name="sliding">Optional sliding flag overriding configuration.</param>
        /// <param name="ct">Cancellation token.</param>
        Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, bool? sliding = null, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a cached value or populates it using the provided factory when not present.
        /// </summary>
        /// <typeparam name="T">Type of the cached payload.</typeparam>
        /// <param name="key">Cache key without any provider-specific prefix.</param>
        /// <param name="factory">Factory that produces the value when the cache miss occurs.</param>
        /// <param name="ttl">Optional explicit duration override.</param>
        /// <param name="sliding">Optional sliding flag override.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The cached or newly produced value.</returns>
        Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, bool? sliding = null, CancellationToken ct = default);

        /// <summary>
        /// Removes an entry from the cache if present.
        /// </summary>
        /// <param name="key">Cache key without any provider-specific prefix.</param>
        /// <param name="ct">Cancellation token.</param>
        Task RemoveAsync(string key, CancellationToken ct = default);

        /// <summary>
        /// Determines whether a cache entry exists for the provided key.
        /// </summary>
        /// <param name="key">Cache key without any provider-specific prefix.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> when the key exists; otherwise <c>false</c>.</returns>
        Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    }
}
