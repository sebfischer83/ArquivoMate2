using System;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Models
{
    /// <summary>
    /// Describes the contextual information that can be supplied to the LLM when
    /// answering an ad-hoc user question about a document.
    /// </summary>
    public sealed class DocumentQuestionContext
    {
        /// <summary>
        /// Identifier of the document that acts as the knowledge source.
        /// </summary>
        public Guid DocumentId { get; init; }

        /// <summary>
        /// Title of the document if available.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Optional natural language summary.
        /// </summary>
        public string? Summary { get; init; }

        /// <summary>
        /// Keywords generated for the document.
        /// </summary>
        public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Full textual content of the document.
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Language hint (ISO code) detected for the document.
        /// </summary>
        public string? Language { get; init; }

        /// <summary>
        /// Optional recent history entries describing important events for the document.
        /// </summary>
        public IReadOnlyList<string> History { get; init; } = Array.Empty<string>();
    }
}
