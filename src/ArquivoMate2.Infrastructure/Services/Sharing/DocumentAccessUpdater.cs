using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces.Sharing;
using ArquivoMate2.Domain.Sharing; // ShareGroup
using ArquivoMate2.Shared.Models.Sharing; // ShareTargetType enum
using ArquivoMate2.Infrastructure.Persistance;
using Marten;
using ArquivoMate2.Application.Interfaces; // ISearchClient

namespace ArquivoMate2.Infrastructure.Services.Sharing;

internal sealed class DocumentAccessUpdater : IDocumentAccessUpdater
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _querySession;
    private readonly ISearchClient _searchClient;

    public DocumentAccessUpdater(IDocumentSession session, IQuerySession querySession, ISearchClient searchClient)
    {
        _session = session;
        _querySession = querySession;
        _searchClient = searchClient;
    }

    public async Task AddShareAsync(DocumentShare share, CancellationToken cancellationToken = default)
    {
        var view = await _querySession.LoadAsync<DocumentAccessView>(share.DocumentId, cancellationToken) ?? new DocumentAccessView
        {
            Id = share.DocumentId,
            OwnerUserId = share.OwnerUserId
        };

        // Ensure owner always in effective users
        view.EffectiveUserIds.Add(view.OwnerUserId);

        switch (share.Target.Type)
        {
            case ShareTargetType.User:
                if (view.DirectUserIds.Add(share.Target.Identifier))
                {
                    view.EffectiveUserIds.Add(share.Target.Identifier);
                }
                break;
            case ShareTargetType.Group:
                if (view.GroupIds.Add(share.Target.Identifier))
                {
                    var group = await _querySession.Query<ShareGroup>()
                        .Where(g => g.Id == share.Target.Identifier)
                        .Select(g => new { g.MemberUserIds })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (group != null)
                    {
                        foreach (var member in group.MemberUserIds)
                        {
                            view.EffectiveUserIds.Add(member);
                        }
                    }
                }
                break;
        }

        view.DirectUserShareCount = view.DirectUserIds.Count;
        view.GroupShareCount = view.GroupIds.Count;

        _session.Store(view);
        await _session.SaveChangesAsync(cancellationToken);

        // Update search index access
        var allowed = view.EffectiveUserIds.Where(u => !string.Equals(u, view.OwnerUserId, StringComparison.Ordinal)).Distinct().ToArray();
        await _searchClient.UpdateDocumentAccessAsync(view.Id, allowed, cancellationToken);
    }

    public async Task RemoveShareAsync(DocumentShare share, CancellationToken cancellationToken = default)
    {
        var view = await _querySession.LoadAsync<DocumentAccessView>(share.DocumentId, cancellationToken);
        if (view == null)
        {
            return; // nothing to do
        }

        var changed = false;
        switch (share.Target.Type)
        {
            case ShareTargetType.User:
                if (view.DirectUserIds.Remove(share.Target.Identifier))
                {
                    changed = true;
                }
                break;
            case ShareTargetType.Group:
                if (view.GroupIds.Remove(share.Target.Identifier))
                {
                    changed = true;
                }
                break;
        }

        if (!changed)
        {
            return; // share not represented
        }

        // Recompute effective user set (owner + direct users + all members of groups)
        var effective = new HashSet<string> { view.OwnerUserId };

        foreach (var u in view.DirectUserIds)
        {
            effective.Add(u);
        }

        if (view.GroupIds.Count > 0)
        {
            var memberSets = await _querySession.Query<ShareGroup>()
                .Where(g => view.GroupIds.Contains(g.Id))
                .Select(g => g.MemberUserIds)
                .ToListAsync(cancellationToken);

            foreach (var set in memberSets)
            {
                foreach (var m in set)
                {
                    effective.Add(m);
                }
            }
        }

        view.EffectiveUserIds = effective;
        view.DirectUserShareCount = view.DirectUserIds.Count;
        view.GroupShareCount = view.GroupIds.Count;

        _session.Store(view);
        await _session.SaveChangesAsync(cancellationToken);

        var allowed = view.EffectiveUserIds.Where(u => !string.Equals(u, view.OwnerUserId, StringComparison.Ordinal)).Distinct().ToArray();
        await _searchClient.UpdateDocumentAccessAsync(view.Id, allowed, cancellationToken);
    }
}
