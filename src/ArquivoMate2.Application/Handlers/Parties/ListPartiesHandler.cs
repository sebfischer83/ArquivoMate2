using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Application.Queries.Parties;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Parties;

public class ListPartiesHandler : IRequestHandler<ListPartiesQuery, IReadOnlyCollection<PartyDto>>
{
    private readonly IQuerySession _querySession;

    public ListPartiesHandler(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    public async Task<IReadOnlyCollection<PartyDto>> Handle(ListPartiesQuery request, CancellationToken cancellationToken)
    {
        var parties = await _querySession
            .Query<PartyInfo>()
            .OrderBy(p => p.CompanyName)
            .ThenBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);

        return parties
            .Select(p => p.ToDto())
            .ToList();
    }
}
