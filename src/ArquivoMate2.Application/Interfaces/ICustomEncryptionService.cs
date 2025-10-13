using ArquivoMate2.Domain.Document;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface ICustomEncryptionService
    {
        Task<(string fullPath, EncryptedArtifactKey? key)> SaveAsync(string userId, Guid documentId, string filename, byte[] content, string artifact, CancellationToken ct = default);
        Task<(string fullPath, EncryptedArtifactKey? key)> SaveAsync(string userId, Guid documentId, string filename, Stream content, string artifact, CancellationToken ct = default);
        bool IsEnabled { get; }
    }
}
