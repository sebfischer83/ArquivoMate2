using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.Collections;
using ArquivoMate2.Domain.Collections;
using ArquivoMate2.Shared.Models.Collections;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Collections;

public sealed class ListCollectionsHandler : IRequestHandler<ListCollectionsQuery, IReadOnlyCollection<CollectionDto>>
{
    private readonly IQuerySession _query;

    public ListCollectionsHandler(IQuerySession query)
    {
        _query = query;
    }

    public async Task<IReadOnlyCollection<CollectionDto>> Handle(ListCollectionsQuery request, CancellationToken cancellationToken)
    {
        var collections = await _query.Query<DocumentCollection>()
            .Where(c => c.OwnerUserId == request.OwnerUserId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        // Preload memberships count per collection (single query)
        var counts = await _query.Query<DocumentCollectionMembership>()
            .Where(m => collections.Select(c => c.Id).Contains(m.CollectionId))
            .GroupBy(m => m.CollectionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var dict = counts.ToDictionary(x => x.Key, x => x.Count);

        return collections.Select(c => new CollectionDto
        {
            Id = c.Id,
            Name = c.Name,
            CreatedAtUtc = c.CreatedAtUtc,
            DocumentCount = dict.TryGetValue(c.Id, out var cnt) ? cnt : 0
        }).ToList();
    }
}
