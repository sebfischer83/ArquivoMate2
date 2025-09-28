using MediatR;
using ArquivoMate2.Shared.Models.Notes;

namespace ArquivoMate2.Application.Commands.Notes
{
    public record CreateDocumentNoteCommand(Guid DocumentId, string UserId, string Text) : IRequest<DocumentNoteDto>;
}
