using ArquivoMate2.Application.Models;
using ArquivoMate2.Application.Queries.Parties;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Parties;

public class GetPartyHandler : IRequestHandler<GetPartyQuery, PartyDto?>
{
    private readonly IQuerySession _querySession;

    public GetPartyHandler(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    public async Task<PartyDto?> Handle(GetPartyQuery request, CancellationToken cancellationToken)
    {
        var party = await _querySession.LoadAsync<PartyInfo>(request.Id, cancellationToken);
        return party?.ToDto();
    }
}
