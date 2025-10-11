using System;

namespace ArquivoMate2.Application.Models
{
    /// <summary>
    /// Represents a contiguous slice of a document that is used for retrieval
    /// and tool-based grounding when answering questions.
    /// </summary>
    public sealed class DocumentChunk
    {
        /// <summary>
        /// Deterministic identifier (e.g. "chunk_1").
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// Zero-based chunk index for ordering.
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// Inclusive start offset within the original document content.
        /// </summary>
        public int Start { get; init; }

        /// <summary>
        /// Exclusive end offset within the original document content.
        /// </summary>
        public int End { get; init; }

        /// <summary>
        /// Raw text contained within the chunk.
        /// </summary>
        public string Content { get; init; } = string.Empty;
    }
}
