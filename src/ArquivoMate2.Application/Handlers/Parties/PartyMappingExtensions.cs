using ArquivoMate2.Application.Models;
using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.Application.Handlers.Parties;

internal static class PartyMappingExtensions
{
    public static PartyDto ToDto(this PartyInfo party)
        => new()
        {
            Id = party.Id,
            FirstName = party.FirstName,
            LastName = party.LastName,
            CompanyName = party.CompanyName,
            Street = party.Street,
            HouseNumber = party.HouseNumber,
            PostalCode = party.PostalCode,
            City = party.City
        };
}
