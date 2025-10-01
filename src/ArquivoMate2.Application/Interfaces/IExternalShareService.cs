using ArquivoMate2.Domain.Sharing;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IExternalShareService
    {
        Task<ExternalShare> CreateAsync(Guid documentId, string ownerUserId, string artifact, TimeSpan ttl, CancellationToken ct);
        Task<ExternalShare?> GetAsync(Guid shareId, CancellationToken ct);
    }
}
