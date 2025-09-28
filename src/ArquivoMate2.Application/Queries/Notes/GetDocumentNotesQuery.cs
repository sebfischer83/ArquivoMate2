using MediatR;
using ArquivoMate2.Shared.Models.Notes;

namespace ArquivoMate2.Application.Queries.Notes
{
    public record GetDocumentNotesQuery(Guid DocumentId, string UserId, string? Search) : IRequest<IReadOnlyList<DocumentNoteDto>>;
}
