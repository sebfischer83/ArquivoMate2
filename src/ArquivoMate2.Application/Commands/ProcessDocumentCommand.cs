using ArquivoMate2.Domain.Document;
using MediatR;

namespace ArquivoMate2.Application.Commands
{
    public record ProcessDocumentCommand(Guid DocumentId, string UserId) : IRequest<Document?>;
}
