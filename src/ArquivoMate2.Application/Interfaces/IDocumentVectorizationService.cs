using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Handles storage and retrieval of semantic document representations used for
    /// retrieval-augmented generation.
    /// </summary>
    public interface IDocumentVectorizationService
    {
        /// <summary>
        /// Splits the provided document content into chunks, generates embeddings and
        /// persists them inside the configured vector store.
        /// </summary>
        Task StoreDocumentAsync(Guid documentId, string userId, string content, CancellationToken cancellationToken);

        /// <summary>
        /// Removes all vector representations for the given document.
        /// </summary>
        Task DeleteDocumentAsync(Guid documentId, string userId, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the most relevant chunk identifiers for the supplied question based on
        /// a similarity search inside the vector store.
        /// </summary>
        Task<IReadOnlyList<string>> FindRelevantChunkIdsAsync(Guid documentId, string userId, string question, int limit, CancellationToken cancellationToken);
    }
}
