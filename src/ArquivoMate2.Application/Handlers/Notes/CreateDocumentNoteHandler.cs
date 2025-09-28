using ArquivoMate2.Application.Commands.Notes;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Notes;
using ArquivoMate2.Shared.Models.Notes;
using Marten;
using MediatR;
using AutoMapper;

namespace ArquivoMate2.Application.Handlers.Notes
{
    public class CreateDocumentNoteHandler : IRequestHandler<CreateDocumentNoteCommand, DocumentNoteDto>
    {
        private readonly IDocumentSession _session;
        private readonly IMapper _mapper;

        public CreateDocumentNoteHandler(IDocumentSession session, IMapper mapper)
        {
            _session = session;
            _mapper = mapper;
        }

        public async Task<DocumentNoteDto> Handle(CreateDocumentNoteCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                throw new ArgumentException("Text is required", nameof(request.Text));
            if (request.Text.Length > 2000)
                throw new ArgumentException("Text length exceeds 2000 characters", nameof(request.Text));

            // Aggregate the document from its event stream to get owner & current NotesCount
            var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: cancellationToken);
            if (doc == null)
                throw new KeyNotFoundException("Document not found");
            if (doc.UserId != request.UserId)
                throw new UnauthorizedAccessException("Not allowed to add notes to this document");
            if (doc.NotesCount >= 10)
                throw new InvalidOperationException("Maximum number of notes (10) reached");

            var note = new DocumentNote
            {
                Id = Guid.NewGuid(),
                DocumentId = request.DocumentId,
                UserId = request.UserId,
                Text = request.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _session.Store(note);
            _session.Events.Append(request.DocumentId, new DocumentNoteAdded(request.DocumentId, note.Id, request.UserId, DateTime.UtcNow));
            await _session.SaveChangesAsync(cancellationToken);

            return _mapper.Map<DocumentNoteDto>(note);
        }
    }
}
