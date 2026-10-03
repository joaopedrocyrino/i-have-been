using IHaveBeen.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IHaveBeen.Infrastructure.Health;

internal sealed class GarageHealthCheck(GarageObjectStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try { await storage.CheckAvailabilityAsync(ct); return HealthCheckResult.Healthy(); }
        catch { return HealthCheckResult.Unhealthy(); }
    }
}
