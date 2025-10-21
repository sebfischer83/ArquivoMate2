using MediatR;

namespace ArquivoMate2.Application.Commands
{
    public record SetDocumentAcceptedCommand(Guid DocumentId, bool Accepted, string UserId) : IRequest<bool>;
}
