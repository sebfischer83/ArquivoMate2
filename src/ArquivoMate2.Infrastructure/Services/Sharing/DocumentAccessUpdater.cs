using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces.Sharing;
using ArquivoMate2.Domain.Sharing; // ShareGroup
using ArquivoMate2.Shared.Models.Sharing; // ShareTargetType enum
using ArquivoMate2.Domain.ReadModels;
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

        EnsureCollections(view);

        var permissions = NormalizePermissions(share.Permissions);

        switch (share.Target.Type)
        {
            case ShareTargetType.User:
                MergePermissions(view.DirectUserPermissions, share.Target.Identifier, permissions);
                break;
            case ShareTargetType.Group:
                MergePermissions(view.GroupPermissions, share.Target.Identifier, permissions);
                break;
        }

        await RecalculateEffectiveUsersAsync(view, cancellationToken);

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

        EnsureCollections(view);

        switch (share.Target.Type)
        {
            case ShareTargetType.User:
                view.DirectUserPermissions.Remove(share.Target.Identifier);
                break;
            case ShareTargetType.Group:
                view.GroupPermissions.Remove(share.Target.Identifier);
                break;
        }

        await RecalculateEffectiveUsersAsync(view, cancellationToken);

        _session.Store(view);
        await _session.SaveChangesAsync(cancellationToken);

        var allowed = view.EffectiveUserIds.Where(u => !string.Equals(u, view.OwnerUserId, StringComparison.Ordinal)).Distinct().ToArray();
        await _searchClient.UpdateDocumentAccessAsync(view.Id, allowed, cancellationToken);
    }

    private static void EnsureCollections(DocumentAccessView view)
    {
        view.DirectUserPermissions ??= new Dictionary<string, DocumentPermissions>(StringComparer.Ordinal);
        view.GroupPermissions ??= new Dictionary<string, DocumentPermissions>(StringComparer.Ordinal);
        view.EffectiveUserPermissions ??= new Dictionary<string, DocumentPermissions>(StringComparer.Ordinal);
        view.EffectiveUserIds ??= new HashSet<string>(StringComparer.Ordinal);
        view.EffectiveEditUserIds ??= new HashSet<string>(StringComparer.Ordinal);
        view.EffectiveDeleteUserIds ??= new HashSet<string>(StringComparer.Ordinal);
    }

    private static DocumentPermissions NormalizePermissions(DocumentPermissions permissions)
    {
        if (permissions == DocumentPermissions.None)
        {
            return DocumentPermissions.Read;
        }

        if (!permissions.HasFlag(DocumentPermissions.Read))
        {
            permissions |= DocumentPermissions.Read;
        }

        return permissions;
    }

    private static void MergePermissions(IDictionary<string, DocumentPermissions> assignments, string identifier, DocumentPermissions permissions)
    {
        permissions = NormalizePermissions(permissions);

        if (assignments.TryGetValue(identifier, out var existing))
        {
            assignments[identifier] = existing | permissions;
            return;
        }

        assignments[identifier] = permissions;
    }

    private async Task RecalculateEffectiveUsersAsync(DocumentAccessView view, CancellationToken cancellationToken)
    {
        EnsureCollections(view);

        var effectivePermissions = new Dictionary<string, DocumentPermissions>(StringComparer.Ordinal)
        {
            [view.OwnerUserId] = DocumentPermissions.All
        };

        foreach (var assignment in view.DirectUserPermissions)
        {
            MergePermissions(effectivePermissions, assignment.Key, assignment.Value);
        }

        if (view.GroupPermissions.Count > 0)
        {
            var groupIds = view.GroupPermissions.Keys.ToArray();
            var groups = await _querySession.Query<ShareGroup>()
                .Where(g => groupIds.Contains(g.Id))
                .Select(g => new { g.Id, g.MemberUserIds })
                .ToListAsync(cancellationToken);

            foreach (var group in groups)
            {
                if (!view.GroupPermissions.TryGetValue(group.Id, out var permissions))
                {
                    continue;
                }

                foreach (var member in group.MemberUserIds)
                {
                    MergePermissions(effectivePermissions, member, permissions);
                }
            }
        }

        view.EffectiveUserPermissions = effectivePermissions;
        view.EffectiveUserIds = new HashSet<string>(effectivePermissions
            .Where(kv => kv.Value.HasFlag(DocumentPermissions.Read))
            .Select(kv => kv.Key), StringComparer.Ordinal);
        view.EffectiveEditUserIds = new HashSet<string>(effectivePermissions
            .Where(kv => kv.Value.HasFlag(DocumentPermissions.Edit))
            .Select(kv => kv.Key), StringComparer.Ordinal);
        view.EffectiveDeleteUserIds = new HashSet<string>(effectivePermissions
            .Where(kv => kv.Value.HasFlag(DocumentPermissions.Delete))
            .Select(kv => kv.Key), StringComparer.Ordinal);
        view.DirectUserShareCount = view.DirectUserPermissions.Count;
        view.GroupShareCount = view.GroupPermissions.Count;
    }
}
