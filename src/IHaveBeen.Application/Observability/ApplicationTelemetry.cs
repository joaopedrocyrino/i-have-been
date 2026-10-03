using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace IHaveBeen.Application.Observability;

// Only fixed operation/outcome names enter telemetry. Never pass business data here.
public static class ApplicationTelemetry
{
    public const string Name = "IHaveBeen";
    public static readonly ActivitySource Activities = new(Name);
    public static readonly Meter Meter = new(Name);
    private static readonly Histogram<double> DependencyDuration = Meter.CreateHistogram<double>("ihb.dependency.duration", "s");
    private static readonly Counter<long> Uploads = Meter.CreateCounter<long>("ihb.uploads", "{upload}");
    public static DependencyMeasurement Dependency(DependencyOperation operation) => new(operation);
    public static void Upload(UploadOutcome outcome) => Uploads.Add(1, new KeyValuePair<string, object?>("outcome", outcome.ToString().ToLowerInvariant()));

    public sealed class DependencyMeasurement : IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly string operation;
        private readonly Activity? activity;
        private string outcome = "error";
        internal DependencyMeasurement(DependencyOperation operation)
        {
            this.operation = operation.ToString().ToLowerInvariant();
            activity = Activities.StartActivity(this.operation, ActivityKind.Client);
            activity?.SetTag("dependency.operation", this.operation);
        }
        public void Complete(DependencyOutcome result = DependencyOutcome.Success) => outcome = result.ToString().ToLowerInvariant();
        public void Dispose()
        {
            activity?.SetTag("outcome", outcome);
            if (outcome is "error" or "unavailable") activity?.SetStatus(ActivityStatusCode.Error);
            DependencyDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds,
                new KeyValuePair<string, object?>("dependency.operation", operation), new KeyValuePair<string, object?>("outcome", outcome));
            activity?.Dispose();
        }
    }
}
public enum DependencyOperation { PostgresQuery, GaragePut, GarageOpen, GarageDelete, MalwareScan }
public enum DependencyOutcome { Success, Rejected, Unavailable }
public enum UploadOutcome { Accepted, Quota, Size, Invalid, Malware, ScannerUnavailable, NotFound, Forbidden }
