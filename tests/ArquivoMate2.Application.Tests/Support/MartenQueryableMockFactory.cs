using System;
using System.Collections.Generic;
using System.Linq;
using Marten.Linq;
using Moq;

namespace ArquivoMate2.Application.Tests.Support;

internal static class MartenQueryableMockFactory
{
    public static IMartenQueryable<T> Create<T>(IEnumerable<T> source)
    {
        var q = source.AsQueryable();
        var mock = new Mock<IMartenQueryable<T>>();

        // Make the mock also behave like IQueryable<T>
        mock.As<IQueryable<T>>().SetupGet(x => x.ElementType).Returns(q.ElementType);
        mock.As<IQueryable<T>>().SetupGet(x => x.Expression).Returns(q.Expression);
        mock.As<IQueryable<T>>().SetupGet(x => x.Provider).Returns(q.Provider);
        mock.As<IEnumerable<T>>().Setup(x => x.GetEnumerator()).Returns(() => q.GetEnumerator());

        return mock.Object;
    }
}
