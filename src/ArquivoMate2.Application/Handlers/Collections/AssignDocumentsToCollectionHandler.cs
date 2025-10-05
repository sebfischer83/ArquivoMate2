using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Collections;
using ArquivoMate2.Application.Interfaces.Sharing;
using ArquivoMate2.Domain.Collections;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Collections;

public sealed class AssignDocumentsToCollectionHandler : IRequestHandler<AssignDocumentsToCollectionCommand, int>
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _query;
    private readonly IDocumentOwnershipLookup _ownership;

    public AssignDocumentsToCollectionHandler(IDocumentSession session, IQuerySession query, IDocumentOwnershipLookup ownership)
    {
        _session = session;
        _query = query;
        _ownership = ownership;
    }

    public async Task<int> Handle(AssignDocumentsToCollectionCommand request, CancellationToken cancellationToken)
    {
        if (request.DocumentIds is null || request.DocumentIds.Count == 0) return 0;

        var collection = await _query.LoadAsync<DocumentCollection>(request.CollectionId, cancellationToken);
        if (collection is null || !string.Equals(collection.OwnerUserId, request.OwnerUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("Collection not found or access denied.");

        // Load existing memberships for these docs to avoid duplicates
        var docIds = request.DocumentIds.Distinct().ToList();
        var existing = await _query.Query<DocumentCollectionMembership>()
            .Where(m => m.CollectionId == request.CollectionId && docIds.Contains(m.DocumentId))
            .Select(m => m.DocumentId)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet();

        int created = 0;
        foreach (var id in docIds)
        {
            if (existingSet.Contains(id)) continue;
            var ownership = await _ownership.GetAsync(id, cancellationToken);
            if (ownership is null || ownership.Value.Deleted || !string.Equals(ownership.Value.UserId, request.OwnerUserId, StringComparison.Ordinal))
                continue;

            var membership = new DocumentCollectionMembership
            {
                CollectionId = request.CollectionId,
                DocumentId = id,
                OwnerUserId = request.OwnerUserId,
                CreatedAtUtc = DateTime.UtcNow
            };
            _session.Store(membership);
            created++;
        }

        if (created > 0)
            await _session.SaveChangesAsync(cancellationToken);

        return created;
    }
}
