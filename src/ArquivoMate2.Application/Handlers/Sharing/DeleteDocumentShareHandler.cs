using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Infrastructure.Persistance;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Sharing;

public class DeleteDocumentShareHandler : IRequestHandler<DeleteDocumentShareCommand, bool>
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _querySession;

    public DeleteDocumentShareHandler(IDocumentSession session, IQuerySession querySession)
    {
        _session = session;
        _querySession = querySession;
    }

    public async Task<bool> Handle(DeleteDocumentShareCommand request, CancellationToken cancellationToken)
    {
        var documentInfo = await _querySession.Query<DocumentView>()
            .Where(d => d.Id == request.DocumentId && !d.Deleted)
            .Select(d => new { d.Id, d.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (documentInfo is null || !string.Equals(documentInfo.UserId, request.OwnerUserId, StringComparison.Ordinal))
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
        return true;
    }
}
