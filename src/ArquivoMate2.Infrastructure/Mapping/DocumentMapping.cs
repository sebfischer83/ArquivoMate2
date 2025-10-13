using ArquivoMate2.Domain.ReadModels;
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
using Marten;
using ArquivoMate2.Application.Models;

namespace ArquivoMate2.Infrastructure.Mapping
{
    public class DocumentListItemMapping : Profile
    {
        public DocumentListItemMapping()
        {
            CreateMap<DocumentView, DocumentListItemDto>()
                    .ForMember(dest => dest.ThumbnailPath, opt => opt.MapFrom<PathResolver, string>(src => src.ThumbnailPath))
                    .ForMember(dest => dest.Encrypted, opt => opt.MapFrom(src => src.Encrypted)) // NEW
                    .ForMember(dest => dest.Sender, opt => opt.MapFrom<PartyListResolver, Guid?>(src => src.SenderId));
        }
    }

    public class DocumentMapping : Profile
    {
        public DocumentMapping()
        {
            CreateMap<DocumentView, DocumentDto>()
                .ForMember(d => d.Id, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.Encrypted, o => o.MapFrom(s => s.Encrypted))
                .ForMember(d => d.EncryptionType, o => o.MapFrom(s => (DocumentEncryptionType)s.EncryptionType))
                    .ForMember(dest => dest.FilePath, opt => opt.MapFrom<PathResolver, string>(src => src.FilePath))
                    .ForMember(dest => dest.ThumbnailPath, opt => opt.MapFrom<PathResolver, string>(src => src.ThumbnailPath))
                    .ForMember(dest => dest.MetadataPath, opt => opt.MapFrom<PathResolver, string>(src => src.MetadataPath))
                    .ForMember(dest => dest.PreviewPath, opt => opt.MapFrom<PathResolver, string>(src => src.PreviewPath))
                    .ForMember(dest => dest.ArchivePath, opt => opt.MapFrom<PathResolver, string>(src => src.ArchivePath)) // NEW
                    .ForMember(dest => dest.UploadedAt, opt => opt.MapFrom(src => src.UploadedAt))
                    .ForMember(dest => dest.ProcessedAt, opt => opt.MapFrom(src => src.ProcessedAt))
                    .ForMember(dest => dest.ChatBotModel, opt => opt.MapFrom(src => src.ChatBotModel))
                    .ForMember(dest => dest.ChatBotClass, opt => opt.MapFrom(src => src.ChatBotClass))
                    .ForMember(dest => dest.NotesCount, opt => opt.MapFrom(src => src.NotesCount)) // NEW
                    .ForMember(dest => dest.Language, opt => opt.MapFrom(src => src.Language)) // NEW
                    .ForMember(dest => dest.Sender, opt => opt.MapFrom<PartyResolver, Guid?>(src => src.SenderId)); // resolve sender

            // Resolve sender PartyDto if SenderId present in DocumentView (new column required in view)
            CreateMap<DocumentView, PartyDto>()
                .ForMember(d => d.Id, o => o.Ignore());
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

    public class PartyResolver : IMemberValueResolver<DocumentView, DocumentDto, Guid?, PartyDto?>
    {
        private readonly IQuerySession _query;

        public PartyResolver(IQuerySession query)
        {
            _query = query;
        }

        public PartyDto? Resolve(DocumentView source, DocumentDto destination, Guid? sourceMember, PartyDto? destMember, ResolutionContext context)
            => ResolveInternal(sourceMember);

        private PartyDto? ResolveInternal(Guid? sourceMember)
        {
            if (!sourceMember.HasValue) return null;
            // load party info directly without filtering by owner user id
            var party = _query.Query<PartyInfo>().FirstOrDefault(p => p.Id == sourceMember.Value);
            if (party == null) return null;
            return new PartyDto
            {
                Id = party.Id,
                FirstName = party.FirstName,
                LastName = party.LastName,
                CompanyName = party.CompanyName,
                Street = party.Street,
                HouseNumber = party.HouseNumber,
                PostalCode = party.PostalCode,
                City = party.City
            };
        }
    }

    // Lightweight resolver for list items: only send Id and DisplayName
    public class PartyListResolver : IMemberValueResolver<DocumentView, DocumentListItemDto, Guid?, PartyListDto?>
    {
        private readonly IQuerySession _query;

        public PartyListResolver(IQuerySession query)
        {
            _query = query;
        }

        public PartyListDto? Resolve(DocumentView source, DocumentListItemDto destination, Guid? sourceMember, PartyListDto? destMember, ResolutionContext context)
        {
            if (!sourceMember.HasValue) return null;
            // load party directly without user filter
            var party = _query.Query<PartyInfo>().FirstOrDefault(p => p.Id == sourceMember.Value);
            if (party == null) return null;
            var display = string.IsNullOrWhiteSpace(party.CompanyName)
                ? string.Join(' ', new[] { party.FirstName, party.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim()
                : party.CompanyName;
            return new PartyListDto { Id = party.Id, DisplayName = display };
        }
    }
}
