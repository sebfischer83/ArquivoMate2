using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Collections;
using ArquivoMate2.Domain.Collections;
using ArquivoMate2.Shared.Models.Collections;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Collections;

public sealed class UpdateCollectionHandler : IRequestHandler<UpdateCollectionCommand, CollectionDto?>
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _query;

    public UpdateCollectionHandler(IDocumentSession session, IQuerySession query)
    {
        _session = session;
        _query = query;
    }

    public async Task<CollectionDto?> Handle(UpdateCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await _session.LoadAsync<DocumentCollection>(request.CollectionId, cancellationToken);
        if (collection is null || !string.Equals(collection.OwnerUserId, request.OwnerUserId, StringComparison.Ordinal))
            return null;

        var trimmed = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Collection name is required", nameof(request.Name));

        var normalized = trimmed.ToUpperInvariant();
        if (!string.Equals(collection.NormalizedName, normalized, StringComparison.Ordinal))
        {
            var exists = await _query.Query<DocumentCollection>()
                .Where(c => c.OwnerUserId == request.OwnerUserId && c.NormalizedName == normalized && c.Id != collection.Id)
                .AnyAsync(cancellationToken);
            if (exists)
                throw new InvalidOperationException("A collection with the same name already exists.");

            collection.Name = trimmed;
            collection.NormalizedName = normalized;
        }

        _session.Store(collection);
        await _session.SaveChangesAsync(cancellationToken);

        var docCount = await _query.Query<DocumentCollectionMembership>()
            .Where(m => m.CollectionId == collection.Id)
            .CountAsync(cancellationToken);

        return new CollectionDto
        {
            Id = collection.Id,
            Name = collection.Name,
            CreatedAtUtc = collection.CreatedAtUtc,
            DocumentCount = docCount
        };
    }
}
