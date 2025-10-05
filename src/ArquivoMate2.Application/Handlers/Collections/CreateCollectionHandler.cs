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

public sealed class CreateCollectionHandler : IRequestHandler<CreateCollectionCommand, CollectionDto>
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _query;

    public CreateCollectionHandler(IDocumentSession session, IQuerySession query)
    {
        _session = session;
        _query = query;
    }

    public async Task<CollectionDto> Handle(CreateCollectionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Collection name is required", nameof(request.Name));

        var normalized = request.Name.Trim().ToUpperInvariant();

        var exists = await _query.Query<DocumentCollection>()
            .Where(c => c.OwnerUserId == request.OwnerUserId && c.NormalizedName == normalized)
            .AnyAsync(cancellationToken);
        if (exists) throw new InvalidOperationException("A collection with the same name already exists.");

        var collection = new DocumentCollection
        {
            OwnerUserId = request.OwnerUserId,
            Name = request.Name.Trim(),
            NormalizedName = normalized,
            CreatedAtUtc = DateTime.UtcNow
        };
        _session.Store(collection);
        await _session.SaveChangesAsync(cancellationToken);

        return new CollectionDto
        {
            Id = collection.Id,
            Name = collection.Name,
            CreatedAtUtc = collection.CreatedAtUtc,
            DocumentCount = 0
        };
    }
}
