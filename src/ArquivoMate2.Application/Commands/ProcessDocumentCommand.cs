using MediatR;

namespace ArquivoMate2.Application.Commands
{
    public record ProcessDocumentCommand(Guid DocumentId) : IRequest;
}
