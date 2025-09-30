using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Shared.Models.Sharing; // ShareTargetType
using Marten;
using MediatR;
using ArquivoMate2.Application.Interfaces.Sharing;

namespace ArquivoMate2.Application.Handlers.Sharing;

public class DeleteDocumentShareHandler : IRequestHandler<DeleteDocumentShareCommand, bool>
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _querySession;
    private readonly IDocumentOwnershipLookup _ownershipLookup;
    private readonly IDocumentAccessUpdater _accessUpdater;

    public DeleteDocumentShareHandler(IDocumentSession session, IQuerySession querySession, IDocumentOwnershipLookup ownershipLookup, IDocumentAccessUpdater accessUpdater)
    {
        _session = session;
        _querySession = querySession;
        _ownershipLookup = ownershipLookup;
        _accessUpdater = accessUpdater;
    }

    public async Task<bool> Handle(DeleteDocumentShareCommand request, CancellationToken cancellationToken)
    {
        var documentInfo = await _ownershipLookup.GetAsync(request.DocumentId, cancellationToken);

        if (documentInfo is null || documentInfo.Value.Deleted || !string.Equals(documentInfo.Value.UserId, request.OwnerUserId, StringComparison.Ordinal))
        {
            return false;
        }

        var share = await _querySession.Query<DocumentShare>()
            .Where(s => s.Id == request.ShareId && s.DocumentId == request.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (share is null)
        {
            return false;
        }

        _session.Delete<DocumentShare>(share.Id);
        await _session.SaveChangesAsync(cancellationToken);

        await _accessUpdater.RemoveShareAsync(share, cancellationToken);
        return true;
    }
}
