using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ArquivoMate2.Infrastructure.Configuration.Caching
{
    /// <summary>
    /// Resolves cache entry options based on glob-like configuration patterns.
    /// </summary>
    public class TtlResolver : ITtlResolver
    {
        private readonly CachingOptions _options;

        public TtlResolver(IOptions<CachingOptions> options)
        {
            _options = options.Value;
        }

        public (TimeSpan Ttl, bool Sliding) Resolve(string key)
        {
            foreach (var kv in _options.PerKey)
            {
                var pattern = "^" + Regex.Escape(kv.Key).Replace("\\*", ".*") + "$";
                if (Regex.IsMatch(key, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    return (TimeSpan.FromSeconds(kv.Value.TtlSeconds), kv.Value.Sliding);
                }
            }

            return (TimeSpan.FromSeconds(_options.DefaultTtlSeconds), _options.DefaultSliding);
        }
    }
}
