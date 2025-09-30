using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models.Sharing;
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

        var sharedIds = await GetSharedDocumentIdsAsync(userId, cancellationToken);
        return sharedIds.Contains(documentId);
    }

    public async Task<IReadOnlyCollection<Guid>> GetSharedDocumentIdsAsync(string userId, CancellationToken cancellationToken)
    {
        var result = new HashSet<Guid>();

        var directShares = await _querySession.Query<DocumentShare>()
            .Where(s => s.Target.Type == ShareTargetType.User && s.Target.Identifier == userId)
            .Select(s => s.DocumentId)
            .ToListAsync(cancellationToken);

        foreach (var docId in directShares)
        {
            result.Add(docId);
        }

        var groupIds = await _querySession.Query<ShareGroup>()
            .Where(g => g.MemberUserIds.Contains(userId))
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        if (groupIds.Count > 0)
        {
            var groupShares = await _querySession.Query<DocumentShare>()
                .Where(s => s.Target.Type == ShareTargetType.Group && groupIds.Contains(s.Target.Identifier))
                .Select(s => s.DocumentId)
                .ToListAsync(cancellationToken);

            foreach (var docId in groupShares)
            {
                result.Add(docId);
            }
        }

        return result.ToList();
    }
}
