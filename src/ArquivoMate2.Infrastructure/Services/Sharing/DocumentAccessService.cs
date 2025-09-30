using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Persistance;
using Marten;

namespace ArquivoMate2.Infrastructure.Services.Sharing;

public class DocumentAccessService : IDocumentAccessService
{
    private readonly IQuerySession _querySession;

    public DocumentAccessService(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    public async Task<bool> HasAccessToDocumentAsync(Guid documentId, string userId, CancellationToken cancellationToken)
    {
        // Owner?
        var ownerId = await _querySession.Query<DocumentView>()
            .Where(d => d.Id == documentId && !d.Deleted)
            .Select(d => d.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId is null)
        {
            return false;
        }

        if (string.Equals(ownerId, userId, StringComparison.Ordinal))
        {
            return true;
        }

        // Check access view
        var exists = await _querySession.Query<DocumentAccessView>()
            .Where(a => a.Id == documentId && a.EffectiveUserIds.Contains(userId))
            .AnyAsync(cancellationToken);

        return exists;
    }

    public async Task<IReadOnlyCollection<Guid>> GetSharedDocumentIdsAsync(string userId, CancellationToken cancellationToken)
    {
        // Now returns documents where user is in EffectiveUserIds but is not the owner (OPTIONAL semantics as before: only shared docs)
        var ids = await _querySession.Query<DocumentAccessView>()
            .Where(a => a.EffectiveUserIds.Contains(userId) && a.OwnerUserId != userId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        return ids;
    }
}
