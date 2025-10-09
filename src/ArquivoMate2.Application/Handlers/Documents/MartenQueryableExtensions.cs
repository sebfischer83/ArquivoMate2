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
        if (query is IMartenQueryable<T> martenQuery && IsMartenProvider(query))
        {
            // Marten's ToListAsync returns IReadOnlyList<T>, so we need to convert it to List<T>
            var result = await martenQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
            return result is List<T> list ? list : result.ToList();
        }

        return await Task.FromResult(query.ToList());
    }

    public static async Task<T?> FirstOrDefaultAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (query is IMartenQueryable<T> martenQuery && IsMartenProvider(query))
        {
            return await martenQuery.FirstOrDefaultAsync(cancellationToken);
        }

        return await Task.FromResult(query.FirstOrDefault());
    }

    public static async Task<long> LongCountAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (query is IMartenQueryable<T> martenQuery && IsMartenProvider(query))
        {
            var count = await martenQuery.CountAsync(cancellationToken);
            return count;
        }

        return await Task.FromResult(query.LongCount());
    }

    public static async Task<int> CountAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (query is IMartenQueryable<T> martenQuery && IsMartenProvider(query))
        {
            return await martenQuery.CountAsync(cancellationToken);
        }

        return await Task.FromResult(query.Count());
    }

    public static async Task<int> SumAsyncFallback<T>(this IQueryable<T> query, Expression<System.Func<T, int>> selector, CancellationToken cancellationToken)
    {
        if (query is IMartenQueryable<T> martenQuery && IsMartenProvider(query))
        {
            return await martenQuery.SumAsync(selector, cancellationToken);
        }

        return await Task.FromResult(query.Sum(selector));
    }

    private static bool IsMartenProvider<T>(IQueryable<T> query)
    {
        var providerAssembly = query.Provider.GetType().Assembly;
        return providerAssembly == typeof(IQuerySession).Assembly;
    }
}
