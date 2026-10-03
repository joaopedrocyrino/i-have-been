using System.Diagnostics;
using System.Net;
using System.Text;
using IHaveBeen.Application.Observability;
using IHaveBeen.Web.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace IHaveBeen.InfrastructureTests;

[CollectionDefinition("Telemetry", DisableParallelization = true)]
public sealed class TelemetryCollection;

[Collection("Telemetry")]
public sealed class TelemetryTests
{
    [Theory]
    [InlineData("file:///tmp/export")]
    [InlineData("http://name:password@collector:4318")]
    [InlineData("https://collector/otlp?token=secret")]
    [InlineData("https://collector/otlp#secret")]
    public void Configuration_rejects_unsafe_endpoints(string endpoint) =>
        Assert.Throws<InvalidOperationException>(() => new TelemetryOptions { Endpoint = endpoint }.Validate());

    [Fact]
    public void Unknown_paths_and_methods_have_bounded_labels()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/private-link/sensitive-id";
        Assert.Equal("unmatched", RequestTelemetryMiddleware.Route(context));
        Assert.Equal("OTHER", RequestTelemetryMiddleware.Method("attacker-controlled-method"));
    }

    [Fact]
    public void Operational_warning_budget_caps_abuse_logs()
    {
        var clock = new TestClock();
        var budget = new OperationalLogBudget(clock);
        Assert.Equal(30, Enumerable.Range(0, 1000).Count(_ => budget.TryAcquire()));
        clock.Now = clock.Now.AddMinutes(1);
        Assert.True(budget.TryAcquire());
    }

    [Fact]
    public async Task Actual_otlp_export_contains_timing_and_fixed_labels_without_private_data()
    {
        using var capture = new CapturingHandler();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Telemetry:Enabled"] = "true", ["Telemetry:TraceSampleRatio"] = "1" });
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddHealthChecks();
        builder.AddObservability();
        builder.Services.Configure<OtlpExporterOptions>(o => o.HttpClientFactory = () => new HttpClient(capture, false));
        await using var app = builder.Build();
        var traces = app.Services.GetRequiredService<TracerProvider>();
        var metrics = app.Services.GetRequiredService<MeterProvider>();
        var logs = app.Services.GetRequiredService<ILoggerFactory>();
        var marker = "PRIVATE-JOURNAL-EMAIL-TOKEN-KEY";
        using var ambient = new Activity(marker).Start();
        ambient.AddBaggage("private", marker);
        ambient.TraceStateString = "private=" + marker;
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/logs/" + marker + "/media";
        context.Request.QueryString = new QueryString("?secret=" + marker);
        context.Request.Headers.Cookie = "ihb.account=" + marker;
        context.Request.Method = "POST";
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/api/logs/{id:guid}/media"), 0, EndpointMetadataCollection.Empty, "upload"));
        Activity? observed = null;
        var middleware = new RequestTelemetryMiddleware(async c =>
        {
            observed = Activity.Current;
            using var scope = logs.CreateLogger("IHaveBeen.Operations").BeginScope(marker);
            logs.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command").LogError("SQL and parameter {Secret}", marker);
            using var operation = ApplicationTelemetry.Dependency(DependencyOperation.GaragePut);
            await Task.Delay(10);
            operation.Complete();
            ApplicationTelemetry.Upload(UploadOutcome.Malware);
            c.Response.StatusCode = 429;
        }, logs, app.Services.GetRequiredService<OperationalLogBudget>());
        await middleware.InvokeAsync(context);
        Assert.NotNull(observed);
        Assert.Equal(default, observed.ParentSpanId);
        Assert.NotEqual(ambient.TraceId, observed.TraceId);
        Assert.Empty(observed.Baggage);
        Assert.Null(observed.TraceStateString);
        Assert.Same(ambient, Activity.Current);
        Assert.True(traces.ForceFlush());
        Assert.True(metrics.ForceFlush());
        Assert.True(app.Services.GetRequiredService<LoggerProvider>().ForceFlush());
        var payload = string.Join("\n", capture.Payloads);
        Assert.DoesNotContain(marker, payload);
        Assert.Contains("/api/logs/{id:guid}/media", payload);
        Assert.Contains("ihb.http.request.duration", payload);
        Assert.Contains("ihb.dependency.duration", payload);
        Assert.Contains("ihb.uploads", payload);
        Assert.Contains("RequestCompleted", payload);
        Assert.Contains("i-have-been", payload);
        Assert.Contains("/v1/traces", capture.Paths);
        Assert.Contains("/v1/metrics", capture.Paths);
        Assert.Contains("/v1/logs", capture.Paths);
    }

    [Fact]
    public async Task Unavailable_exporter_does_not_fail_or_delay_request_processing()
    {
        using var capture = new CapturingHandler { Fail = true };
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Telemetry:Enabled"] = "true", ["Telemetry:TraceSampleRatio"] = "1", ["Telemetry:ExportTimeoutMilliseconds"] = "500" });
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddHealthChecks();
        builder.AddObservability();
        builder.Services.Configure<OtlpExporterOptions>(o => o.HttpClientFactory = () => new HttpClient(capture, false));
        await using var app = builder.Build();
        var traces = app.Services.GetRequiredService<TracerProvider>();
        var middleware = new RequestTelemetryMiddleware(c => { c.Response.StatusCode = 200; return Task.CompletedTask; },
            app.Services.GetRequiredService<ILoggerFactory>(), app.Services.GetRequiredService<OperationalLogBudget>());
        var context = new DefaultHttpContext();
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < 1000; i++) await middleware.InvokeAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(2));
        traces.ForceFlush(1000); // Export failure remains independent of the completed requests.
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public bool Fail { get; init; }
        public List<string> Payloads { get; } = [];
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(Send(request, ct));
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken ct)
        {
            if (Fail) throw new HttpRequestException("Collector unavailable");
            var bytes = request.Content!.ReadAsByteArrayAsync(ct).GetAwaiter().GetResult();
            lock (Payloads) { Payloads.Add(Encoding.UTF8.GetString(bytes)); Paths.Add(request.RequestUri!.AbsolutePath); }
            Assert.Null(request.Headers.Authorization);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        }
    }
}
