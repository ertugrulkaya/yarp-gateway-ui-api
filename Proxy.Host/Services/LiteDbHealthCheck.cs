using Microsoft.Extensions.Diagnostics.HealthChecks;
using Proxy.Host.Models;

namespace Proxy.Host.Services;

public class LiteDbHealthCheck : IHealthCheck
{
    private readonly LiteDbService _liteDb;
    private readonly LogService _logService;

    public LiteDbHealthCheck(LiteDbService liteDb, LogService logService)
    {
        _liteDb = liteDb;
        _logService = logService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify both databases are readable
            _liteDb.Database.GetCollection<RouteConfigWrapper>("routes").Count();
            _logService.GetTotalCount();
            return Task.FromResult(HealthCheckResult.Healthy("LiteDB is reachable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("LiteDB unreachable.", ex));
        }
    }
}
