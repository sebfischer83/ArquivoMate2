using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Commands.Parties;

public record UpdatePartyCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Street,
    string? HouseNumber,
    string? PostalCode,
    string? City) : IRequest<PartyDto?>;
