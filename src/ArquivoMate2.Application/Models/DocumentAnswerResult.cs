using System;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Models
{
    /// <summary>
    /// Represents the result of asking the chatbot a question about a document.
    /// </summary>
    public sealed class DocumentAnswerResult
    {
        /// <summary>
        /// Natural language answer provided by the LLM.
        /// </summary>
        public string Answer { get; init; } = string.Empty;

        /// <summary>
        /// Model identifier used to create the answer.
        /// </summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// Optional list of citations/snippets that were referenced.
        /// </summary>
        public IReadOnlyList<DocumentAnswerCitation> Citations { get; init; } = Array.Empty<DocumentAnswerCitation>();
    }

    /// <summary>
    /// Describes a snippet of text that was relevant for an answer.
    /// </summary>
    public sealed class DocumentAnswerCitation
    {
        /// <summary>
        /// Optional label or identifier of the snippet (e.g. page or section).
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// The snippet text itself.
        /// </summary>
        public string Snippet { get; init; } = string.Empty;
    }
}
