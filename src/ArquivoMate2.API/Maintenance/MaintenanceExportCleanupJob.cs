using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.API.Maintenance;

public sealed class MaintenanceExportCleanupJob
{
    private readonly IDocumentEncryptionKeysExportStore _store;
    private readonly ILogger<MaintenanceExportCleanupJob> _logger;

    public MaintenanceExportCleanupJob(
        IDocumentEncryptionKeysExportStore store,
        ILogger<MaintenanceExportCleanupJob> logger)
    {
        _store = store;
        _logger = logger;
    }

    [Queue("maintenance")]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deleted = await _store.DeleteOlderThanAsync(TimeSpan.FromHours(24), cancellationToken);
        if (deleted > 0)
        {
            _logger.LogInformation("Removed {Count} expired maintenance export files", deleted);
        }
    }
}
