using ArquivoMate2.Application.Commands.Parties;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Parties;

public class CreatePartyHandler : IRequestHandler<CreatePartyCommand, PartyDto>
{
    private readonly IDocumentSession _session;

    public CreatePartyHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<PartyDto> Handle(CreatePartyCommand request, CancellationToken cancellationToken)
    {
        var party = new PartyInfo
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName?.Trim() ?? string.Empty,
            LastName = request.LastName?.Trim() ?? string.Empty,
            CompanyName = request.CompanyName?.Trim() ?? string.Empty,
            Street = request.Street?.Trim() ?? string.Empty,
            HouseNumber = request.HouseNumber?.Trim() ?? string.Empty,
            PostalCode = request.PostalCode?.Trim() ?? string.Empty,
            City = request.City?.Trim() ?? string.Empty
        };

        _session.Store(party);
        await _session.SaveChangesAsync(cancellationToken);

        return party.ToDto();
    }
}
