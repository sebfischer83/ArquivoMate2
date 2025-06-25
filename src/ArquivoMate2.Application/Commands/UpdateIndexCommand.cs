using ArquivoMate2.Domain.Document;
using MediatR;

namespace ArquivoMate2.Application.Commands
{
    public record UpdateIndexCommand(Guid DocumentId, Document Document) : IRequest<bool>;
}
