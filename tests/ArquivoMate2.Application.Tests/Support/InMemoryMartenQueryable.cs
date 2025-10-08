using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Marten;
using Marten.Linq;

namespace ArquivoMate2.Application.Tests.Support;

internal sealed class InMemoryMartenQueryable<T> : IMartenQueryable<T>
{
    private readonly IQueryable<T> _queryable;

    public InMemoryMartenQueryable(IEnumerable<T> source)
    {
        _queryable = source.AsQueryable();
    }

    public Type ElementType => _queryable.ElementType;

    public Expression Expression => _queryable.Expression;

    public IQueryProvider Provider => _queryable.Provider;

    public IEnumerator<T> GetEnumerator() => _queryable.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IMartenSession Session => throw new NotImplementedException();

    public string? StatisticsText => null;

    public IMartenQueryable<T> Include<TInclude>(Expression<Func<T, object>> includeExpression) => this;

    public IMartenQueryable<T> Include<TInclude>(Expression<Func<T, object>> includeExpression, JoinType joinType) => this;

    public IMartenQueryable<T> Stats(out QueryStatistics? stats)
    {
        stats = null;
        return this;
    }

    public IMartenQueryable<T> Stats(out QueryStatistics? stats, Action<IQuerySession, IEnumerable<T>> onEnumerate)
    {
        stats = null;
        onEnumerate(default!, Enumerable.Empty<T>());
        return this;
    }

    public IMartenQueryable<T> StreamStats(out QueryStatistics stats)
    {
        stats = new QueryStatistics();
        return this;
    }
}
