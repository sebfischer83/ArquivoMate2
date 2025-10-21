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
            var existingNames = existing.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var added = 0;
            foreach (var rawName in _options.Seed)
            {
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    continue;
                }

                var name = rawName.Trim();
                if (existingNames.Contains(name))
                {
                    continue;
                }

                session.Store(new DocumentTypeDefinition
                {
                    Name = name,
                    IsLocked = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
                added++;
            }

            if (added > 0)
            {
                await session.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Seeded {Count} document types.", added);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
