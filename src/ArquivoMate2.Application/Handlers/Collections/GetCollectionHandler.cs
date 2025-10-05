using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.Collections;
using ArquivoMate2.Domain.Collections;
using ArquivoMate2.Shared.Models.Collections;
using Marten;
using MediatR;
using System.Linq;

namespace ArquivoMate2.Application.Handlers.Collections;

public sealed class GetCollectionHandler : IRequestHandler<GetCollectionQuery, CollectionDto?>
{
    private readonly IQuerySession _query;

    public GetCollectionHandler(IQuerySession query)
    {
        _query = query;
    }

    public async Task<CollectionDto?> Handle(GetCollectionQuery request, CancellationToken cancellationToken)
    {
        var collection = await _query.LoadAsync<DocumentCollection>(request.CollectionId, cancellationToken);
        if (collection is null || !string.Equals(collection.OwnerUserId, request.OwnerUserId, System.StringComparison.Ordinal))
            return null;

        var count = await _query.Query<DocumentCollectionMembership>()
            .Where(m => m.CollectionId == request.CollectionId)
            .CountAsync(cancellationToken);

        return new CollectionDto
        {
            Id = collection.Id,
            Name = collection.Name,
            CreatedAtUtc = collection.CreatedAtUtc,
            DocumentCount = count
        };
    }
}
