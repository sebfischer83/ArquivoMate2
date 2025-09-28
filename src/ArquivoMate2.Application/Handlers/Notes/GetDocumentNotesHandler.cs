using ArquivoMate2.Application.Queries.Notes;
using ArquivoMate2.Domain.Notes;
using ArquivoMate2.Shared.Models.Notes;
using AutoMapper;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Notes
{
    public class GetDocumentNotesHandler : IRequestHandler<GetDocumentNotesQuery, IReadOnlyList<DocumentNoteDto>>
    {
        private readonly IQuerySession _query;
        private readonly IMapper _mapper;

        public GetDocumentNotesHandler(IQuerySession query, IMapper mapper)
        {
            _query = query;
            _mapper = mapper;
        }

        public async Task<IReadOnlyList<DocumentNoteDto>> Handle(GetDocumentNotesQuery request, CancellationToken cancellationToken)
        {
            var q = _query.Query<DocumentNote>().Where(n => n.DocumentId == request.DocumentId);
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim().ToLowerInvariant();
                q = q.Where(n => n.Text.ToLower().Contains(term));
            }
            var list = await q.OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
            return _mapper.Map<IReadOnlyList<DocumentNoteDto>>(list);
        }
    }
}
