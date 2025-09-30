using MediatR;

namespace ArquivoMate2.Application.Commands.Sharing;

public record DeleteShareGroupCommand(string GroupId, string OwnerUserId) : IRequest<bool>;
