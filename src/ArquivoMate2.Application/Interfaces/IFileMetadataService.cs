using ArquivoMate2.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IFileMetadataService
    {
        Task WriteMetadataAsync(DocumentMetadata metadata, CancellationToken ct = default);
        Task<DocumentMetadata?> ReadMetadataAsync(Guid documentId, string userId, CancellationToken ct = default);

        /// <summary>
        /// Deserializes document metadata from a raw JSON byte array.
        /// Returns null when the input is empty or deserialization fails.
        /// </summary>
        Task<DocumentMetadata?> ReadMetadataAsync(byte[] data, CancellationToken ct = default);
    }
}
