using MediatR;

namespace ArquivoMate2.Application.Commands.Notes
{
    public record DeleteDocumentNoteCommand(Guid DocumentId, Guid NoteId, string UserId) : IRequest<bool>;
}
