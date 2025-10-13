using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models;
using AutoMapper;

namespace ArquivoMate2.Infrastructure.Mapping
{
    /// <summary>
    /// Resolves the encryption mode reported by document DTOs based on the
    /// per-document projection state and the globally configured defaults.
    /// </summary>
    public class DocumentEncryptionResolver :
        IValueResolver<DocumentView, DocumentDto, DocumentEncryptionType>,
        IValueResolver<DocumentView, DocumentListItemDto, DocumentEncryptionType>
    {
        private readonly IDocumentEncryptionDescriptor _descriptor;

        public DocumentEncryptionResolver(IDocumentEncryptionDescriptor descriptor)
        {
            _descriptor = descriptor;
        }

        public DocumentEncryptionType Resolve(
            DocumentView source,
            DocumentDto destination,
            DocumentEncryptionType destMember,
            ResolutionContext context)
            => ResolveCore(source);

        public DocumentEncryptionType Resolve(
            DocumentView source,
            DocumentListItemDto destination,
            DocumentEncryptionType destMember,
            ResolutionContext context)
            => ResolveCore(source);

        private DocumentEncryptionType ResolveCore(DocumentView source)
            => source.Encryption == DocumentEncryptionType.Custom
                ? DocumentEncryptionType.Custom
                : _descriptor.DefaultEncryption;
    }
}
