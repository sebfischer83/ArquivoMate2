using ArquivoMate2.Domain.Sharing;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IExternalShareService
    {
        Task<ExternalShare> CreateAsync(Guid documentId, string ownerUserId, string artifact, TimeSpan ttl, CancellationToken ct);
        Task<ExternalShare?> GetAsync(Guid shareId, CancellationToken ct);

        // List all public shares for a given document
        Task<IReadOnlyCollection<ExternalShare>> ListByDocumentAsync(Guid documentId, CancellationToken ct);

        // Delete a specific share by id. Returns true if deleted.
        Task<bool> DeleteAsync(Guid shareId, CancellationToken ct);

        // Delete all expired shares and return count removed
        Task<int> DeleteExpiredAsync(CancellationToken ct);
    }
}
