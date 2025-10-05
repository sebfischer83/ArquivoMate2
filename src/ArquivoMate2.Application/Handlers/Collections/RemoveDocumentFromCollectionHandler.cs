using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Collections;
using ArquivoMate2.Domain.Collections;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Collections;

public sealed class RemoveDocumentFromCollectionHandler : IRequestHandler<RemoveDocumentFromCollectionCommand, bool>
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _query;

    public RemoveDocumentFromCollectionHandler(IDocumentSession session, IQuerySession query)
    {
        _session = session;
        _query = query;
    }

    public async Task<bool> Handle(RemoveDocumentFromCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await _query.LoadAsync<DocumentCollection>(request.CollectionId, cancellationToken);
        if (collection is null || !string.Equals(collection.OwnerUserId, request.OwnerUserId, StringComparison.Ordinal))
            return false;

        var membership = await _query.Query<DocumentCollectionMembership>()
            .Where(m => m.CollectionId == request.CollectionId && m.DocumentId == request.DocumentId)
            .Select(m => new { m.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null) return false;

        _session.Delete<DocumentCollectionMembership>(membership.Id);
        await _session.SaveChangesAsync(cancellationToken);
        return true;
    }
}
