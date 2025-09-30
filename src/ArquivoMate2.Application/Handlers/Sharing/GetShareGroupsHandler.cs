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

public class GetShareGroupsHandler : IRequestHandler<GetShareGroupsQuery, IReadOnlyCollection<ShareGroupDto>>
{
    private readonly IQuerySession _querySession;

    public GetShareGroupsHandler(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    public async Task<IReadOnlyCollection<ShareGroupDto>> Handle(GetShareGroupsQuery request, CancellationToken cancellationToken)
    {
        var groups = await _querySession.Query<ShareGroup>()
            .Where(g => g.OwnerUserId == request.OwnerUserId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return groups.Select(g => new ShareGroupDto
        {
            Id = g.Id,
            Name = g.Name,
            MemberUserIds = g.MemberUserIds
        }).ToList();
    }
}
