using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Sharing;
using Marten;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.Infrastructure.Services.Sharing
{
    public class ExternalShareService : IExternalShareService
    {
        private readonly IDocumentSession _session;
        private readonly IQuerySession _query;
        private readonly AppSettings _appSettings;
        private readonly ILogger<ExternalShareService> _logger;

        public ExternalShareService(IDocumentSession session, IQuerySession query, AppSettings appSettings, ILogger<ExternalShareService> logger)
        {
            _session = session;
            _query = query;
            _appSettings = appSettings;
            _logger = logger;
        }

        public async Task<ExternalShare> CreateAsync(Guid documentId, string ownerUserId, string artifact, TimeSpan ttl, CancellationToken ct)
        {
            // Use DateTimeKind.Unspecified when storing into Postgres timestamp without time zone to avoid Npgsql UTC-kind restriction
            var nowUtc = DateTime.UtcNow;
            var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Unspecified);

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

            _logger.LogInformation("External share {ShareId} created for document {DocumentId} by user {UserId}, expires at {Expires}", share.Id, documentId, ownerUserId, share.ExpiresAtUtc);

            return share;
        }

        public Task<ExternalShare?> GetAsync(Guid shareId, CancellationToken ct) => _query.LoadAsync<ExternalShare>(shareId, ct);

        public async Task<IReadOnlyCollection<ExternalShare>> ListByDocumentAsync(Guid documentId, CancellationToken ct)
        {
            var shares = await _query.Query<ExternalShare>()
                .Where(s => s.DocumentId == documentId)
                .OrderBy(s => s.CreatedAtUtc)
                .ToListAsync(ct);

            _logger.LogDebug("Listed {Count} external shares for document {DocumentId}", shares.Count, documentId);

            return shares;
        }

        public async Task<bool> DeleteAsync(Guid shareId, CancellationToken ct)
        {
            var share = await _query.LoadAsync<ExternalShare>(shareId, ct);
            if (share == null) return false;
            _session.Delete<ExternalShare>(shareId);
            await _session.SaveChangesAsync(ct);

            _logger.LogInformation("External share {ShareId} for document {DocumentId} deleted", shareId, share.DocumentId);
            return true;
        }

        public async Task<int> DeleteExpiredAsync(CancellationToken ct)
        {
            // Use DateTimeKind.Unspecified to avoid Npgsql rejecting UTC-kind DateTime for timestamptz/"timestamp without time zone" comparisons
            var nowUtc = DateTime.UtcNow;
            var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Unspecified);

            var expired = await _query.Query<ExternalShare>()
                .Where(s => s.ExpiresAtUtc < now || s.Revoked)
                .ToListAsync(ct);

            if (expired.Count == 0) return 0;

            await using var s = _session.DocumentStore.LightweightSession();
            foreach (var item in expired)
            {
                s.Delete(item);
            }
            await s.SaveChangesAsync(ct);

            _logger.LogInformation("Removed {Count} expired or revoked external shares", expired.Count);
            return expired.Count;
        }
    }
}
