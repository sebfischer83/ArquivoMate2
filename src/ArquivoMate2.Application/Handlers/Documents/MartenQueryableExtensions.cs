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
            // Try to call an async ToListAsync on the concrete query via reflection (supports test in-memory helpers)
            var mi = query.GetType().GetMethod("ToListAsync", new[] { typeof(CancellationToken) });
            if (mi != null)
            {
                var task = (System.Threading.Tasks.Task)mi.Invoke(query, new object[] { cancellationToken })!;
                await task.ConfigureAwait(false);
                // get Result property
                var resultProp = task.GetType().GetProperty("Result");
                if (resultProp != null)
                {
                    var res = resultProp.GetValue(task);
                    if (res is System.Collections.IEnumerable enumerable)
                        return enumerable.Cast<T>().ToList();
                }
            }
        }

        return await Task.FromResult(query.ToList());
    }

    public static async Task<T?> FirstOrDefaultAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (IsMartenProvider(query))
        {
            var mi = query.GetType().GetMethod("FirstOrDefaultAsync", new[] { typeof(CancellationToken) });
            if (mi != null)
            {
                var task = (System.Threading.Tasks.Task)mi.Invoke(query, new object[] { cancellationToken })!;
                await task.ConfigureAwait(false);
                var resultProp = task.GetType().GetProperty("Result");
                if (resultProp != null) return (T?)resultProp.GetValue(task);
            }
        }

        return await Task.FromResult(query.FirstOrDefault());
    }

    public static async Task<long> LongCountAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (IsMartenProvider(query))
        {
            var mi = query.GetType().GetMethod("CountAsync", new[] { typeof(CancellationToken) });
            if (mi != null)
            {
                var task = (System.Threading.Tasks.Task)mi.Invoke(query, new object[] { cancellationToken })!;
                await task.ConfigureAwait(false);
                var resultProp = task.GetType().GetProperty("Result");
                if (resultProp != null)
                {
                    var val = resultProp.GetValue(task);
                    if (val is int i) return i;
                    if (val is long l) return l;
                }
            }
        }

        return await Task.FromResult(query.LongCount());
    }

    public static async Task<int> CountAsyncFallback<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (IsMartenProvider(query))
        {
            var mi = query.GetType().GetMethod("CountAsync", new[] { typeof(CancellationToken) });
            if (mi != null)
            {
                var task = (System.Threading.Tasks.Task)mi.Invoke(query, new object[] { cancellationToken })!;
                await task.ConfigureAwait(false);
                var resultProp = task.GetType().GetProperty("Result");
                if (resultProp != null) return Convert.ToInt32(resultProp.GetValue(task));
            }
        }

        return await Task.FromResult(query.Count());
    }

    public static async Task<int> SumAsyncFallback<T>(this IQueryable<T> query, Expression<System.Func<T, int>> selector, CancellationToken cancellationToken)
    {
        if (IsMartenProvider(query))
        {
            var mi = query.GetType().GetMethod("SumAsync", new[] { selector.GetType(), typeof(CancellationToken) });
            if (mi != null)
            {
                var task = (System.Threading.Tasks.Task)mi.Invoke(query, new object[] { selector, cancellationToken })!;
                await task.ConfigureAwait(false);
                var resultProp = task.GetType().GetProperty("Result");
                if (resultProp != null) return Convert.ToInt32(resultProp.GetValue(task));
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
