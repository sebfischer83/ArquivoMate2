using System;
using System.Threading;
using System.Threading.Tasks;
using Meilisearch;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.API.HealthChecks;

public class MeilisearchHealthCheck : IHealthCheck
{
    private readonly MeilisearchClient _client;
    private readonly ILogger<MeilisearchHealthCheck> _logger;

    public MeilisearchHealthCheck(MeilisearchClient client, ILogger<MeilisearchHealthCheck> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var healthResponse = await _client.HealthAsync();

            if (healthResponse.Status == "available")
            {
                return HealthCheckResult.Healthy("Meilisearch is reachable.");
            }

            return HealthCheckResult.Unhealthy("Meilisearch did not report a healthy status.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meilisearch health check failed.");
            return HealthCheckResult.Unhealthy("Meilisearch is unreachable.", ex);
        }
    }
}
