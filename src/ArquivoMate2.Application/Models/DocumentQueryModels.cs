using System;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Models
{
    /// <summary>
    /// Describes a structured document query that the chatbot can execute via tooling.
    /// </summary>
    public sealed class DocumentQuery
    {
        /// <summary>
        /// Optional free-text search expression to narrow down the result set.
        /// </summary>
        public string? Search { get; init; }

        /// <summary>
        /// Additional metadata filters applied to the query.
        /// </summary>
        public DocumentQueryFilters Filters { get; init; } = new();

        /// <summary>
        /// Determines which parts of the query result should be returned.
        /// </summary>
        public DocumentQueryProjection Projection { get; init; } = DocumentQueryProjection.Documents;

        /// <summary>
        /// Maximum number of document hits that should be returned.
        /// </summary>
        public int Limit { get; init; } = 10;
    }

    /// <summary>
    /// Encapsulates optional filter values for document queries.
    /// </summary>
    public sealed class DocumentQueryFilters
    {
        /// <summary>
        /// Restricts the query to specific documents.
        /// </summary>
        public IReadOnlyList<Guid> DocumentIds { get; init; } = Array.Empty<Guid>();

        /// <summary>
        /// Restricts the result to documents whose logical date belongs to the specified year.
        /// </summary>
        public int? Year { get; init; }

        /// <summary>
        /// Lower bound for the original file size in megabytes.
        /// </summary>
        public double? MinFileSizeMb { get; init; }

        /// <summary>
        /// Upper bound for the original file size in megabytes.
        /// </summary>
        public double? MaxFileSizeMb { get; init; }

        /// <summary>
        /// Optional document type label to filter by.
        /// </summary>
        public string? Type { get; init; }
    }

    /// <summary>
    /// Specifies which parts of the query result should be materialized.
    /// </summary>
    public enum DocumentQueryProjection
    {
        Documents,
        Count,
        Both
    }

    /// <summary>
    /// Holds the outcome of executing a <see cref="DocumentQuery"/>.
    /// </summary>
    public sealed class DocumentQueryResult
    {
        /// <summary>
        /// List of documents that matched the query.
        /// </summary>
        public IReadOnlyList<DocumentQueryDocument> Documents { get; init; } = Array.Empty<DocumentQueryDocument>();

        /// <summary>
        /// Number of documents that match the query (if requested).
        /// </summary>
        public long? TotalCount { get; init; }
    }

    /// <summary>
    /// Represents a single document hit returned by <see cref="DocumentQueryResult"/>.
    /// </summary>
    public sealed class DocumentQueryDocument
    {
        public Guid DocumentId { get; init; }

        public string? Title { get; init; }

        public string? Summary { get; init; }

        public DateTime? Date { get; init; }

        public double? Score { get; init; }

        public long? FileSizeBytes { get; init; }
    }
}
