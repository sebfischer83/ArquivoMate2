using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models.Sharing;
using Marten;

namespace ArquivoMate2.Infrastructure.Services.Sharing;

public class AutoShareService : IAutoShareService
{
    private readonly IDocumentStore _documentStore;

    public AutoShareService(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task ApplyRulesAsync(Guid documentId, string ownerUserId, CancellationToken cancellationToken)
    {
        await using var query = _documentStore.QuerySession();

        var rules = await query.Query<ShareAutomationRule>()
            .Where(r => r.OwnerUserId == ownerUserId && r.Scope != ShareAutomationScope.Filtered)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return;
        }

        var existingShares = await query.Query<DocumentShare>()
            .Where(s => s.DocumentId == documentId)
            .Select(s => new { s.Target.Type, s.Target.Identifier })
            .ToListAsync(cancellationToken);

        var existingKeys = new HashSet<string>(existingShares.Select(s => ComposeKey(s.Type, s.Identifier)));

        var toCreate = new List<DocumentShare>();
        foreach (var rule in rules)
        {
            var key = ComposeKey(rule.Target.Type, rule.Target.Identifier);
            if (existingKeys.Contains(key))
            {
                continue;
            }

            toCreate.Add(new DocumentShare
            {
                DocumentId = documentId,
                OwnerUserId = ownerUserId,
                Target = new ShareTarget
                {
                    Type = rule.Target.Type,
                    Identifier = rule.Target.Identifier
                },
                SharedAt = DateTime.UtcNow,
                GrantedBy = ownerUserId,
                Permissions = rule.Permissions
            });
        }

        if (toCreate.Count > 0)
        {
            await using var session = _documentStore.LightweightSession();
            session.Store(toCreate.ToArray());
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ApplyRuleToExistingDocumentsAsync(ShareAutomationRule rule, CancellationToken cancellationToken)
    {
        await using var query = _documentStore.QuerySession();

        var documentIds = await query.Query<DocumentView>()
            .Where(d => d.UserId == rule.OwnerUserId && !d.Deleted)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        if (documentIds.Count == 0)
        {
            return;
        }

        var existingShares = await query.Query<DocumentShare>()
            .Where(s => documentIds.Contains(s.DocumentId) && s.Target.Type == rule.Target.Type && s.Target.Identifier == rule.Target.Identifier)
            .Select(s => s.DocumentId)
            .ToListAsync(cancellationToken);

        var missingDocumentIds = documentIds.Except(existingShares).ToList();
        if (missingDocumentIds.Count == 0)
        {
            return;
        }

        var toCreate = missingDocumentIds.Select(id => new DocumentShare
        {
            DocumentId = id,
            OwnerUserId = rule.OwnerUserId,
            Target = new ShareTarget
            {
                Type = rule.Target.Type,
                Identifier = rule.Target.Identifier
            },
            SharedAt = DateTime.UtcNow,
            GrantedBy = rule.OwnerUserId,
            Permissions = rule.Permissions
        }).ToList();

        await using var session = _documentStore.LightweightSession();
        session.Store(toCreate.ToArray());
        await session.SaveChangesAsync(cancellationToken);
    }

    private static string ComposeKey(ShareTargetType type, string identifier) => $"{(int)type}:{identifier}";
}
