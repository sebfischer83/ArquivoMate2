using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Shared.Models.Sharing;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Sharing;

public class CreateShareAutomationRuleHandler : IRequestHandler<CreateShareAutomationRuleCommand, ShareAutomationRuleDto>
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _querySession;
    private readonly IAutoShareService _autoShareService;

    public CreateShareAutomationRuleHandler(IDocumentSession session, IQuerySession querySession, IAutoShareService autoShareService)
    {
        _session = session;
        _querySession = querySession;
        _autoShareService = autoShareService;
    }

    public async Task<ShareAutomationRuleDto> Handle(CreateShareAutomationRuleCommand request, CancellationToken cancellationToken)
    {
        if (request.Target is null || string.IsNullOrWhiteSpace(request.Target.Identifier))
        {
            throw new ArgumentException("Share target is required", nameof(request.Target));
        }

        if (request.Scope == ShareAutomationScope.Filtered)
        {
            throw new NotSupportedException("Filtered automation rules are not supported yet.");
        }

        if (request.Target.Type == ShareTargetType.Group)
        {
            var group = await _querySession.Query<ShareGroup>()
                .Where(g => g.Id == request.Target.Identifier)
                .FirstOrDefaultAsync(cancellationToken);

            if (group is null || !string.Equals(group.OwnerUserId, request.OwnerUserId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Group not found or access denied.");
            }
        }

        var exists = await _querySession.Query<ShareAutomationRule>()
            .Where(r => r.OwnerUserId == request.OwnerUserId && r.Target.Type == request.Target.Type && r.Target.Identifier == request.Target.Identifier)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("An automation rule for this target already exists.");
        }

        var permissions = NormalizePermissions(request.Permissions);

        var rule = new ShareAutomationRule
        {
            OwnerUserId = request.OwnerUserId,
            Target = new ShareTarget
            {
                Type = request.Target.Type,
                Identifier = request.Target.Identifier
            },
            Scope = request.Scope,
            Permissions = permissions
        };

        _session.Store(rule);
        await _session.SaveChangesAsync(cancellationToken);

        if (request.Scope == ShareAutomationScope.AllDocuments)
        {
            await _autoShareService.ApplyRuleToExistingDocumentsAsync(rule, cancellationToken);
        }

        return new ShareAutomationRuleDto
        {
            Id = rule.Id,
            Target = rule.Target,
            Scope = rule.Scope,
            Permissions = rule.Permissions
        };
    }

    private static DocumentPermissions NormalizePermissions(DocumentPermissions permissions)
    {
        if (permissions == DocumentPermissions.None)
        {
            throw new InvalidOperationException("Automation rules must grant at least read access.");
        }

        if (!permissions.HasFlag(DocumentPermissions.Read))
        {
            permissions |= DocumentPermissions.Read;
        }

        return permissions;
    }
}
