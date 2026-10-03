using System.Diagnostics;
using System.Diagnostics.Metrics;
using IHaveBeen.Application.Observability;
using Microsoft.AspNetCore.Routing;

namespace IHaveBeen.Web.Observability;

internal sealed class RequestTelemetryMiddleware(RequestDelegate next, ILoggerFactory logs, OperationalLogBudget budget)
{
    private static readonly Histogram<double> Duration = ApplicationTelemetry.Meter.CreateHistogram<double>("ihb.http.request.duration", "s");
    private static readonly UpDownCounter<long> Active = ApplicationTelemetry.Meter.CreateUpDownCounter<long>("ihb.http.active_requests", "{request}");
    private readonly ILogger operations = logs.CreateLogger("IHaveBeen.Operations");
    public async Task InvokeAsync(HttpContext context)
    {
        // Never ingest caller-supplied baggage/tracestate or use caller sampling decisions.
        var ambient = Activity.Current;
        Activity.Current = null;
        var activity = ApplicationTelemetry.Activities.StartActivity("request", ActivityKind.Server);
        var started = Stopwatch.GetTimestamp();
        Active.Add(1);
        var failureStatus = 0;
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { failureStatus = 499; throw; }
        catch { failureStatus = 500; throw; }
        finally
        {
            var route = Route(context);
            var method = Method(context.Request.Method);
            var status = failureStatus == 0 ? context.Response.StatusCode : failureStatus;
            activity?.SetTag("http.route", route);
            activity?.SetTag("http.request.method", method);
            activity?.SetTag("http.response.status_code", status);
            if (activity is not null) activity.DisplayName = method + " " + route;
            if (status >= 500) activity?.SetStatus(ActivityStatusCode.Error);
            // Long-lived Blazor connections have a separate population from API latency.
            var kind = context.Request.Path.StartsWithSegments("/_blazor") ? "connection" :
                context.Request.Path.StartsWithSegments("/health") ? "health" : "request";
            Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds,
                new TagList { { "http.route", route }, { "http.request.method", method }, { "http.response.status_code", status }, { "request.kind", kind } });
            Active.Add(-1);
            if ((status >= 500 || status == 429) && budget.TryAcquire())
                operations.LogWarning("RequestCompleted {Route} {Method} {StatusCode}", route, method, status);
            activity?.Dispose();
            Activity.Current = ambient;
        }
    }
    internal static string Route(HttpContext context)
    {
        // Only route patterns defined in source, never raw paths or route values.
        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        return pattern is { Length: > 0 and <= 200 } ? pattern : "unmatched";
    }
    internal static string Method(string method) => method switch
    { "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" => method, _ => "OTHER" };
}
