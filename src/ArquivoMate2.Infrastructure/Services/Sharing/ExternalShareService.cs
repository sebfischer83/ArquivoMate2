using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Sharing;
using Marten;

namespace ArquivoMate2.Infrastructure.Services.Sharing
{
    public class ExternalShareService : IExternalShareService
    {
        private readonly IDocumentSession _session;
        private readonly AppSettings _appSettings;

        public ExternalShareService(IDocumentSession session, AppSettings appSettings)
        {
            _session = session;
            _appSettings = appSettings;
        }

        public async Task<ExternalShare> CreateAsync(Guid documentId, string ownerUserId, string artifact, TimeSpan ttl, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var share = new ExternalShare
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Artifact = artifact,
                OwnerUserId = ownerUserId,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(ttl),
                Revoked = false
            };
            _session.Store(share);
            await _session.SaveChangesAsync(ct);
            return share;
        }

        public Task<ExternalShare?> GetAsync(Guid shareId, CancellationToken ct) => _session.LoadAsync<ExternalShare>(shareId, ct);
    }
}
