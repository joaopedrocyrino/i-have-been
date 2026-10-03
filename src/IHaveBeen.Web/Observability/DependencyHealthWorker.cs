using System.Diagnostics.Metrics;
using IHaveBeen.Application.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IHaveBeen.Web.Observability;

internal sealed class DependencyHealthWorker(HealthCheckService health, ILoggerFactory logs) : BackgroundService
{
    private readonly ILogger operations = logs.CreateLogger("IHaveBeen.Operations");
    private readonly Dictionary<string, int> previous = new();
    private static readonly Histogram<double> Duration = ApplicationTelemetry.Meter.CreateHistogram<double>("ihb.healthcheck.duration", "s");
    private static readonly Gauge<int> Healthy = ApplicationTelemetry.Meter.CreateGauge<int>("ihb.dependency.healthy", "{dependency}");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        do
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var report = await health.CheckHealthAsync(timeout.Token);
                foreach (var name in new[] { "postgres", "garage", "malware-scanner" })
                {
                    var found = report.Entries.TryGetValue(name, out var entry);
                    var ok = found && entry.Status == HealthStatus.Healthy ? 1 : 0;
                    Healthy.Record(ok, new KeyValuePair<string, object?>("dependency", name));
                    if (found) Duration.Record(entry.Duration.TotalSeconds, new KeyValuePair<string, object?>("dependency", name));
                    if ((!previous.TryGetValue(name, out var old) && ok == 0) || (previous.ContainsKey(name) && old != ok))
                        operations.LogWarning("DependencyHealth {Dependency} {Healthy}", name, ok);
                    previous[name] = ok;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch
            {
                foreach (var name in new[] { "postgres", "garage", "malware-scanner" })
                    Healthy.Record(0, new KeyValuePair<string, object?>("dependency", name));
                operations.LogWarning("DependencyHealthCheckUnavailable");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
