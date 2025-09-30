using System;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Domain.Sharing;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Sharing;

public class DeleteShareAutomationRuleHandler : IRequestHandler<DeleteShareAutomationRuleCommand, bool>
{
    private readonly IDocumentSession _session;

    public DeleteShareAutomationRuleHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<bool> Handle(DeleteShareAutomationRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _session.LoadAsync<ShareAutomationRule>(request.RuleId, cancellationToken);
        if (rule is null || !string.Equals(rule.OwnerUserId, request.OwnerUserId, StringComparison.Ordinal))
        {
            return false;
        }

        _session.Delete<ShareAutomationRule>(rule.Id);
        await _session.SaveChangesAsync(cancellationToken);
        return true;
    }
}
