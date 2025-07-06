using ArquivoMate2.Domain.Email;
using ArquivoMate2.Shared.Models;
using AutoMapper;

namespace ArquivoMate2.Infrastructure.Mapping
{
    public class EmailCriteriaMapping : Profile
    {
        public EmailCriteriaMapping()
        {
            CreateMap<ArquivoMate2.Domain.Email.EmailCriteria, EmailCriteriaDto>();
            CreateMap<EmailCriteriaDto, ArquivoMate2.Domain.Email.EmailCriteria>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
            
            CreateMap<SaveEmailCriteriaRequest, ArquivoMate2.Domain.Email.EmailCriteria>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
        }
    }
}