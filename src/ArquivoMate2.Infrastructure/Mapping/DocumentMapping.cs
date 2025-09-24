using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using ArquivoMate2.Application.Interfaces;
using StackExchange.Redis;
using System.IO;

namespace ArquivoMate2.Infrastructure.Mapping
{
    public class DocumentListItemMapping : Profile
    {
        public DocumentListItemMapping()
        {
            CreateMap<DocumentView, DocumentListItemDto>()
                    .ForMember(dest => dest.ThumbnailPath, opt => opt.MapFrom<PathResolver, string>(src => src.ThumbnailPath));
        }
    }

    public class DocumentMapping : Profile
    {
        public DocumentMapping()
        {
            CreateMap<DocumentView, DocumentDto>()
                    .ForMember(dest => dest.FilePath, opt => opt.MapFrom<PathResolver, string>(src => src.FilePath))
                    .ForMember(dest => dest.ThumbnailPath, opt => opt.MapFrom<PathResolver, string>(src => src.ThumbnailPath))
                    .ForMember(dest => dest.MetadataPath, opt => opt.MapFrom<PathResolver, string>(src => src.MetadataPath))
                    .ForMember(dest => dest.PreviewPath, opt => opt.MapFrom<PathResolver, string>(src => src.PreviewPath))
                    .ForMember(dest => dest.UploadedAt, opt => opt.MapFrom(src => src.UploadedAt))
                    .ForMember(dest => dest.ProcessedAt, opt => opt.MapFrom(src => src.ProcessedAt));
        }

    }

    public class PathResolver : IMemberValueResolver<DocumentView, BaseDto, string, string>
    {
        private readonly IDeliveryProvider _storageProvider;

        public PathResolver(IDeliveryProvider storageProvider)
        {
            _storageProvider = storageProvider;
        }

        public string Resolve(DocumentView source, BaseDto destination, string sourceMember, string destMember, ResolutionContext context)
        {
            if (string.IsNullOrEmpty(sourceMember))
                return string.Empty;

            return _storageProvider.GetAccessUrl(sourceMember).GetAwaiter().GetResult();
        }
    }
}
