using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.Parties;

public record GetPartyQuery(Guid Id) : IRequest<PartyDto?>;
