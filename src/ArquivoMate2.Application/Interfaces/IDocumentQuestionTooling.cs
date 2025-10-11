using ArquivoMate2.Application.Models;

namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Provides data-access helpers that a chatbot implementation can call while
    /// composing an answer for document-related questions.
    /// </summary>
    public interface IDocumentQuestionTooling
    {
        /// <summary>
        /// Executes a structured document query on behalf of the language model.
        /// </summary>
        /// <param name="query">Parameters describing the desired documents or aggregations.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the request scope.</param>
        /// <returns>A result containing document hits and/or aggregate statistics.</returns>
        Task<DocumentQueryResult> QueryDocumentsAsync(DocumentQuery query, CancellationToken cancellationToken);
    }
}
