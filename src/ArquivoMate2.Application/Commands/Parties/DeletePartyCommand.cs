using MediatR;

namespace ArquivoMate2.Application.Commands.Parties;

public record DeletePartyCommand(Guid Id) : IRequest<bool>;
