using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using ArquivoMate2.Application.Interfaces;

namespace ArquivoMate2.API.Maintenance;

public sealed class MaintenanceExpiredSharesCleanupJob
{
    private readonly IExternalShareService _shareService;
    private readonly ILogger<MaintenanceExpiredSharesCleanupJob> _logger;

    public MaintenanceExpiredSharesCleanupJob(IExternalShareService shareService, ILogger<MaintenanceExpiredSharesCleanupJob> logger)
    {
        _shareService = shareService;
        _logger = logger;
    }

    [Queue("maintenance")]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _shareService.DeleteExpiredAsync(cancellationToken);
            if (deleted > 0)
            {
                _logger.LogInformation("Removed {Count} expired public shares", deleted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed running expired shares cleanup job");
        }
    }
}
