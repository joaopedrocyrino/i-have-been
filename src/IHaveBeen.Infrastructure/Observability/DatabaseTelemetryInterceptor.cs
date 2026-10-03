using System.Data.Common;
using System.Diagnostics;
using IHaveBeen.Application.Observability;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Diagnostics.Metrics;

namespace IHaveBeen.Infrastructure.Observability;

internal sealed class DatabaseTelemetryInterceptor : DbCommandInterceptor
{
    private static readonly Histogram<double> Duration = ApplicationTelemetry.Meter.CreateHistogram<double>("ihb.database.duration", "s");
    private static void Record(TimeSpan duration, string outcome)
    {
        Duration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));
        // Backdate a child span using EF's execution duration; retain no command text or parameters.
        var ended = DateTimeOffset.UtcNow;
        using var activity = ApplicationTelemetry.Activities.StartActivity("postgresquery", ActivityKind.Client,
            parentContext: default, startTime: ended - duration);
        activity?.SetTag("dependency.operation", "postgresquery");
        activity?.SetTag("outcome", outcome);
        if (outcome == "error") activity?.SetStatus(ActivityStatusCode.Error);
        activity?.SetEndTime(ended.UtcDateTime);
    }
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData data, DbDataReader result) { Record(data.Duration, "success"); return result; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data, DbDataReader result, CancellationToken ct = default) { Record(data.Duration, "success"); return ValueTask.FromResult(result); }
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData data, int result) { Record(data.Duration, "success"); return result; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data, int result, CancellationToken ct = default) { Record(data.Duration, "success"); return ValueTask.FromResult(result); }
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData data, object? result) { Record(data.Duration, "success"); return result; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData data, object? result, CancellationToken ct = default) { Record(data.Duration, "success"); return ValueTask.FromResult(result); }
    public override void CommandFailed(DbCommand command, CommandErrorEventData data) => Record(data.Duration, "error");
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData data, CancellationToken ct = default) { Record(data.Duration, "error"); return Task.CompletedTask; }
    public override void CommandCanceled(DbCommand command, CommandEndEventData data) => Record(data.Duration, "cancelled");
    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData data, CancellationToken ct = default) { Record(data.Duration, "cancelled"); return Task.CompletedTask; }
}
