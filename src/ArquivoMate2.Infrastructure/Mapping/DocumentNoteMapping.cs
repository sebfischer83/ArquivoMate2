using AutoMapper;
using ArquivoMate2.Domain.Notes;
using ArquivoMate2.Shared.Models.Notes;

namespace ArquivoMate2.Infrastructure.Mapping
{
    public class DocumentNoteMapping : Profile
    {
        public DocumentNoteMapping()
        {
            CreateMap<DocumentNote, DocumentNoteDto>();
        }
    }
}
