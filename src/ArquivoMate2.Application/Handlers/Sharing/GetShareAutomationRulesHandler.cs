using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.Sharing;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Shared.Models.Sharing;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Sharing;

public class GetShareAutomationRulesHandler : IRequestHandler<GetShareAutomationRulesQuery, IReadOnlyCollection<ShareAutomationRuleDto>>
{
    private readonly IQuerySession _querySession;

    public GetShareAutomationRulesHandler(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    public async Task<IReadOnlyCollection<ShareAutomationRuleDto>> Handle(GetShareAutomationRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _querySession.Query<ShareAutomationRule>()
            .Where(r => r.OwnerUserId == request.OwnerUserId)
            .OrderBy(r => r.Target.Identifier)
            .ToListAsync(cancellationToken);

        return rules.Select(r => new ShareAutomationRuleDto
        {
            Id = r.Id,
            Target = r.Target,
            Scope = r.Scope,
            Permissions = r.Permissions
        }).ToList();
    }
}
