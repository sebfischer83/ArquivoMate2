using ArquivoMate2.Domain.Document;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Provides a way to obtain the latest DocumentEncryptionKeysAdded event payload for a document.
    /// This abstraction simplifies testing and decouples streaming logic from Marten event types.
    /// </summary>
    public interface IDocumentEncryptionKeysProvider
    {
        Task<DocumentEncryptionKeysAdded?> GetLatestAsync(Guid documentId, CancellationToken ct = default);
    }
}
