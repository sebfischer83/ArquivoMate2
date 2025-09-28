using ArquivoMate2.Application.Commands.Notes;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Notes;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Notes
{
    public class DeleteDocumentNoteHandler : IRequestHandler<DeleteDocumentNoteCommand, bool>
    {
        private readonly IDocumentSession _session;

        public DeleteDocumentNoteHandler(IDocumentSession session)
        {
            _session = session;
        }

        public async Task<bool> Handle(DeleteDocumentNoteCommand request, CancellationToken cancellationToken)
        {
            var note = await _session.LoadAsync<DocumentNote>(request.NoteId, cancellationToken);
            if (note == null || note.DocumentId != request.DocumentId)
                return false;

            var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: cancellationToken);
            if (doc == null)
                return false;

            if (doc.UserId != request.UserId && note.UserId != request.UserId)
                throw new UnauthorizedAccessException("Not allowed to delete this note");

            _session.Delete(note);
            _session.Events.Append(request.DocumentId, new DocumentNoteDeleted(request.DocumentId, note.Id, request.UserId, DateTime.UtcNow));
            await _session.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
