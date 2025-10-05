using ArquivoMate2.Application.Commands.Parties;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Parties;

public class UpdatePartyHandler : IRequestHandler<UpdatePartyCommand, PartyDto?>
{
    private readonly IDocumentSession _session;

    public UpdatePartyHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<PartyDto?> Handle(UpdatePartyCommand request, CancellationToken cancellationToken)
    {
        var party = await _session.LoadAsync<PartyInfo>(request.Id, cancellationToken);
        if (party is null) return null;

        party.FirstName = request.FirstName?.Trim() ?? string.Empty;
        party.LastName = request.LastName?.Trim() ?? string.Empty;
        party.CompanyName = request.CompanyName?.Trim() ?? string.Empty;
        party.Street = request.Street?.Trim() ?? string.Empty;
        party.HouseNumber = request.HouseNumber?.Trim() ?? string.Empty;
        party.PostalCode = request.PostalCode?.Trim() ?? string.Empty;
        party.City = request.City?.Trim() ?? string.Empty;

        _session.Store(party);
        await _session.SaveChangesAsync(cancellationToken);

        return party.ToDto();
    }
}
