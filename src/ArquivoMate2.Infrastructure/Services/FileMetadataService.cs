using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.ValueObjects;
using MediatR;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services
{
    public class FileMetadataService : IFileMetadataService
    {
        private readonly IPathService _paths;

        public FileMetadataService(IPathService paths)
        {
            _paths = paths;
        }

        public async Task WriteMetadataAsync(DocumentMetadata metadata, CancellationToken ct = default)
        {
            var metaPath = Path.Combine(
                _paths.GetDocumentUploadPath(metadata.UserId),
                $"{metadata.DocumentId}.metadata"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);

            var json = JsonSerializer.Serialize(
                metadata,
                new JsonSerializerOptions { WriteIndented = true }
            );
            await File.WriteAllTextAsync(metaPath, json, ct);
        }

        public async Task<DocumentMetadata?> ReadMetadataAsync(Guid documentId, string userId, CancellationToken ct = default)
        {
            var metaPath = Path.Combine(
                 _paths.GetDocumentUploadPath(userId),
                $"{documentId}.metadata"
            );

            if (!File.Exists(metaPath))
                return null;

            var json = await File.ReadAllTextAsync(metaPath, ct);
            return JsonSerializer.Deserialize<DocumentMetadata>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public Task<DocumentMetadata?> ReadMetadataAsync(byte[] data, CancellationToken ct = default)
        {
            if (data == null || data.Length == 0) return Task.FromResult<DocumentMetadata?>(null);
            try
            {
                var readerOptions = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip };
                var json = Encoding.UTF8.GetString(data);
                var md = JsonSerializer.Deserialize<DocumentMetadata>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return Task.FromResult(md);
            }
            catch
            {
                return Task.FromResult<DocumentMetadata?>(null);
            }
        }
    }
}
