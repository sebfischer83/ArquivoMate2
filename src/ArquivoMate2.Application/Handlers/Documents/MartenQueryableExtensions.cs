using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Marten;
using Marten.Linq;

namespace ArquivoMate2.Application.Handlers.Documents;

internal static class MartenQueryableExtensions
{
    public static async Task<List<T>> ToListAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (IsMartenProvider(query))
        {
            // Use Marten's async extension directly when available
            try
            {
                var res = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
                return res.ToList();
            }
            catch
            {
                // Fall through to synchronous fallback
            }
        }

        return await Task.FromResult(query.ToList());
    }

    public static async Task<T?> FirstOrDefaultAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (IsMartenProvider(query))
        {
            try
            {
                return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Fall through to synchronous fallback
            }
        }

        return await Task.FromResult(query.FirstOrDefault());
    }

    public static async Task<long> LongCountAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (IsMartenProvider(query))
        {
            try
            {
                var cnt = await query.CountAsync(cancellationToken).ConfigureAwait(false);
                return cnt;
            }
            catch
            {
                // Fall through to synchronous fallback
            }
        }

        return await Task.FromResult(query.LongCount());
    }

    public static async Task<int> CountAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (IsMartenProvider(query))
        {
            try
            {
                return await query.CountAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Fall through to synchronous fallback
            }
        }

        return await Task.FromResult(query.Count());
    }

    public static async Task<int> SumAsyncFallback<T>(this IQueryable<T> query, Expression<System.Func<T, int>> selector, CancellationToken cancellationToken)
    {
        if (IsMartenProvider(query))
        {
            try
            {
                return await query.SumAsync(selector, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Fall through to synchronous fallback
            }
        }

        return await Task.FromResult(query.Sum(selector));
    }

    private static bool IsMartenProvider<T>(IQueryable<T> query)
    {
        var providerAssembly = query.Provider.GetType().Assembly;
        return providerAssembly == typeof(IQuerySession).Assembly;
    }
}
