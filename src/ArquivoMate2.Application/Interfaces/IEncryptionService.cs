using ArquivoMate2.Domain.Document;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IEncryptionService
    {
        Task<(string fullPath, EncryptedArtifactKey? key)> SaveAsync(string userId, Guid documentId, string filename, byte[] content, string artifact, CancellationToken ct = default);
        bool IsEnabled { get; }
    }
}
