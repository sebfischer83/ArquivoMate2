using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Collections;
using ArquivoMate2.Domain.Collections;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Collections;

public sealed class DeleteCollectionHandler : IRequestHandler<DeleteCollectionCommand, bool>
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _query;

    public DeleteCollectionHandler(IDocumentSession session, IQuerySession query)
    {
        _session = session;
        _query = query;
    }

    public async Task<bool> Handle(DeleteCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await _session.LoadAsync<DocumentCollection>(request.CollectionId, cancellationToken);
        if (collection is null || !string.Equals(collection.OwnerUserId, request.OwnerUserId, System.StringComparison.Ordinal))
            return false;

        // Load memberships to delete
        var memberships = await _query.Query<DocumentCollectionMembership>()
            .Where(m => m.CollectionId == request.CollectionId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in memberships)
            _session.Delete<DocumentCollectionMembership>(id);

        _session.Delete<DocumentCollection>(collection.Id);
        await _session.SaveChangesAsync(cancellationToken);
        return true;
    }
}
