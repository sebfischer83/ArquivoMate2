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

namespace ArquivoMate2.Infrastructure.Mapping
{
    public class DocumentMapping : Profile
    {
        public DocumentMapping()
        {
            CreateMap<DocumentView, DocumentDto>()
                    .ForMember(dest => dest.FilePath, opt => opt.MapFrom<FilePathResolver>())
                    .ForMember(dest => dest.ThumbnailPath, opt => opt.MapFrom<ThumbnailPathResolver>())
                    .ForMember(dest => dest.MetadataPath, opt => opt.MapFrom<MetadataPathResolver>());
        }

    }

    public class FilePathResolver : IValueResolver<DocumentView, DocumentDto, string>
    {
        private readonly IDeliveryProvider _storageProvider;

        public FilePathResolver(IDeliveryProvider storageProvider)
        {
            _storageProvider = storageProvider;
        }

        public string Resolve(DocumentView source, DocumentDto destination, string destMember, ResolutionContext context)
        {
            if (string.IsNullOrEmpty(source.FilePath))
                return string.Empty;

            return Task.Run(async () => await _storageProvider.GetAccessUrl(source.FilePath))
                    .Result;
        }
    }

    public class ThumbnailPathResolver : IValueResolver<DocumentView, DocumentDto, string>
    {
        private readonly IDeliveryProvider _storageProvider;

        public ThumbnailPathResolver(IDeliveryProvider storageProvider)
        {
            _storageProvider = storageProvider;
        }

        public string Resolve(DocumentView source, DocumentDto destination, string destMember, ResolutionContext context)
        {
            if (string.IsNullOrEmpty(source.ThumbnailPath))
                return string.Empty;

            return Task.Run(async () => await _storageProvider.GetAccessUrl(source.ThumbnailPath))
                    .Result;
        }
    }

    public class MetadataPathResolver : IValueResolver<DocumentView, DocumentDto, string>
    {
        private readonly IDeliveryProvider _storageProvider;

        public MetadataPathResolver(IDeliveryProvider storageProvider)
        {
            _storageProvider = storageProvider;
        }

        public string Resolve(DocumentView source, DocumentDto destination, string destMember, ResolutionContext context)
        {
            if (string.IsNullOrEmpty(source.MetadataPath))
                return string.Empty;

            return Task.Run(async () => await _storageProvider.GetAccessUrl(source.MetadataPath))
                    .Result;
        }
    }
}
