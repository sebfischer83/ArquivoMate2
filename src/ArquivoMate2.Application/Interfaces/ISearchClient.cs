using ArquivoMate2.Domain.Document;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Defines operations for synchronizing and querying the search index.
    /// </summary>
    public interface ISearchClient
    {
        /// <summary>
        /// Adds a new document to the search index.
        /// </summary>
        /// <param name="document">Document aggregate to index.</param>
        /// <returns><c>true</c> when the document was queued for indexing.</returns>
        Task<bool> AddDocument(Document document);

        /// <summary>
        /// Updates an existing document in the search index.
        /// </summary>
        /// <param name="document">Document aggregate with the latest data.</param>
        /// <returns><c>true</c> when the update was accepted.</returns>
        Task<bool> UpdateDocument(Document document);

        /// <summary>
        /// Retrieves facet counts for the specified user.
        /// </summary>
        /// <param name="userId">User identifier whose documents should be analyzed.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
        /// <returns>A dictionary keyed by facet name with occurrence counts.</returns>
        Task<Dictionary<string, int>> GetFacetsAsync(string userId, CancellationToken cancellationToken);

        /// <summary>
        /// Searches for document identifiers using full-text search.
        /// </summary>
        /// <param name="userId">User identifier to scope the search.</param>
        /// <param name="search">Search query string.</param>
        /// <param name="page">Page number (1-indexed).</param>
        /// <param name="pageSize">Number of results per page.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
        /// <returns>A tuple containing matching document IDs and the total count.</returns>
        Task<(IReadOnlyList<Guid> Ids, long Total)> SearchDocumentIdsAsync(string userId, string search, int page, int pageSize, CancellationToken cancellationToken);

        // Update read-model access lists without re-indexing content
        /// <summary>
        /// Updates access control information for a document in the search index without re-indexing its content.
        /// </summary>
        /// <param name="documentId">Identifier of the document.</param>
        /// <param name="allowedUserIds">Collection of user IDs that should retain access.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
        Task UpdateDocumentAccessAsync(Guid documentId, IReadOnlyCollection<string> allowedUserIds, CancellationToken cancellationToken);
    }
}
