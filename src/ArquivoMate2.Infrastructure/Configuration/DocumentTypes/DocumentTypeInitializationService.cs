using ArquivoMate2.Domain.DocumentTypes;
using Marten;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Configuration.DocumentTypes
{
    /// <summary>
    /// Hosted service that ensures configured document types exist in the database.
    /// </summary>
    public class DocumentTypeInitializationService : IHostedService
    {
        private readonly IDocumentStore _store;
        private readonly DocumentTypeOptions _options;
        private readonly ILogger<DocumentTypeInitializationService> _logger;

        public DocumentTypeInitializationService(
            IDocumentStore store,
            IOptions<DocumentTypeOptions> options,
            ILogger<DocumentTypeInitializationService> logger)
        {
            _store = store;
            _options = options.Value;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_options.Seed == null || _options.Seed.Count == 0)
            {
                return;
            }

            using var session = _store.LightweightSession();
            var existing = await session.Query<DocumentTypeDefinition>().ToListAsync(cancellationToken);

            var added = 0;
            var updated = 0;
            foreach (var seed in _options.Seed)
            {
                if (seed == null || string.IsNullOrWhiteSpace(seed.Name))
                {
                    continue;
                }

                var name = seed.Name.Trim();
                var existingDef = existing.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                var newFeatures = seed.GetSystemFeatures();

                if (existingDef != null)
                {
                    // Compare sequences (order-insensitive)
                    var existingSet = (existingDef.SystemFeatures ?? new System.Collections.Generic.List<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var newSet = newFeatures.ToHashSet(StringComparer.OrdinalIgnoreCase);

                    if (!existingSet.SetEquals(newSet) || !existingDef.IsLocked)
                    {
                        existingDef.SystemFeatures = newFeatures;
                        existingDef.IsLocked = true;
                        existingDef.UpdatedAtUtc = DateTime.UtcNow;
                        session.Store(existingDef);
                        updated++;
                    }
                    continue;
                }

                session.Store(new DocumentTypeDefinition
                {
                    Name = name,
                    NormalizedName = name.ToUpperInvariant(),
                    SystemFeatures = newFeatures,
                    IsLocked = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
                added++;
            }

            if (added > 0 || updated > 0)
            {
                await session.SaveChangesAsync(cancellationToken);
                if (added > 0) _logger.LogInformation("Seeded {Count} document types.", added);
                if (updated > 0) _logger.LogInformation("Updated {Count} existing document types.", updated);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
