using System;

namespace ArquivoMate2.Infrastructure.Configuration.Caching
{
    public interface ITtlResolver
    {
        (TimeSpan Ttl, bool Sliding) Resolve(string key);
    }
}
