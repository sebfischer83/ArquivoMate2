using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;
using AutoMapper;

namespace ArquivoMate2.Infrastructure.Mapping
{
    public class ImportHistoryMapping : Profile
    {
        public ImportHistoryMapping()
        {
            CreateMap<ImportHistoryView, ImportHistoryListItemDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        }
    }
}
