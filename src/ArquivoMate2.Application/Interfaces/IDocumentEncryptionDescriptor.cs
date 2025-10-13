using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Exposes the default encryption mode applied to stored documents when
    /// no per-document custom encryption is active.
    /// </summary>
    public interface IDocumentEncryptionDescriptor
    {
        /// <summary>
        /// Gets the default encryption mode that should be reported for
        /// documents that are not wrapped by the custom encryption pipeline.
        /// </summary>
        DocumentEncryptionType DefaultEncryption { get; }
    }
}
