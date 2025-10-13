using System.Collections.Generic;

namespace ArquivoMate2.Infrastructure.Configuration.Caching
{
    /// <summary>
    /// Configuration model describing cache defaults and key specific overrides.
    /// </summary>
    public class CachingOptions
    {
        /// <summary>
        /// Optional prefix applied to every cache key.
        /// </summary>
        public string KeyPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Default time-to-live (seconds) when no pattern matches.
        /// </summary>
        public int DefaultTtlSeconds { get; set; } = 300;

        /// <summary>
        /// Default sliding expiration flag.
        /// </summary>
        public bool DefaultSliding { get; set; } = false;

        /// <summary>
        /// Per-key configuration allowing TTL and sliding overrides using glob patterns.
        /// </summary>
        public Dictionary<string, PerKeyTtl> PerKey { get; set; } = new();

        /// <summary>
        /// Redis configuration used for the distributed cache layer.
        /// </summary>
        public RedisOptions Redis { get; set; } = new();

        /// <summary>
        /// OpenTelemetry configuration used to expose cache traces.
        /// </summary>
        public OtelOptions Otel { get; set; } = new();
    }

    public class PerKeyTtl
    {
        public int TtlSeconds { get; set; }
        public bool Sliding { get; set; }
    }

    public class RedisOptions
    {
        public string? Configuration { get; set; }
        public string? InstanceName { get; set; }
    }

    public class OtelOptions
    {
        public string? ServiceName { get; set; }
        public string? Endpoint { get; set; }
    }
}
