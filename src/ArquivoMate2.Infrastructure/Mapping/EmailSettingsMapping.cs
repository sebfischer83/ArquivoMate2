using ArquivoMate2.Domain.Email;
using ArquivoMate2.Shared.Models;
using AutoMapper;

namespace ArquivoMate2.Infrastructure.Mapping
{
    public class EmailSettingsMapping : Profile
    {
        public EmailSettingsMapping()
        {
            CreateMap<EmailSettings, EmailSettingsDto>();
            CreateMap<EmailSettingsDto, EmailSettings>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
        }
    }
}
