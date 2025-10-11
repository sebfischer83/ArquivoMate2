using System;
using System.Collections.Generic;
using ArquivoMate2.Application.Models;

namespace ArquivoMate2.Application.Services.Documents
{
    /// <summary>
    /// Provides deterministic chunking for document content so that all
    /// components share identical chunk identifiers and offsets.
    /// </summary>
    public static class DocumentChunking
    {
        private const int DefaultChunkSize = 1200;

        public static IReadOnlyList<DocumentChunk> Split(string content, int chunkSize = DefaultChunkSize)
        {
            var chunks = new List<DocumentChunk>();
            if (string.IsNullOrEmpty(content))
            {
                return chunks;
            }

            var index = 0;
            var position = 0;
            while (position < content.Length)
            {
                var length = Math.Min(chunkSize, content.Length - position);
                var slice = content.Substring(position, length);
                var id = $"chunk_{++index}";

                chunks.Add(new DocumentChunk
                {
                    Id = id,
                    Index = index - 1,
                    Start = position,
                    End = position + length,
                    Content = slice
                });

                position += length;
            }

            return chunks;
        }
    }
}
