using IHaveBeen.Application.Observability;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace IHaveBeen.Web.Observability;

internal static class TelemetryRegistration
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection("Telemetry").Get<TelemetryOptions>() ?? new();
        builder.Services.AddSingleton(options);
        if (!options.Enabled || builder.Configuration.GetValue<bool>("Telemetry:Migration")) return builder;
        options.Validate();
        builder.Services.AddSingleton<OperationalLogBudget>();
        var resource = ResourceBuilder.CreateEmpty().AddService("i-have-been", serviceVersion: options.ServiceVersion, autoGenerateServiceInstanceId: false)
            .AddAttributes([new("deployment.environment.name", builder.Environment.IsProduction() ? "production" : "development")]);
        void Export(OtlpExporterOptions exporter, string signal)
        {
            exporter.Endpoint = new Uri(options.Endpoint.TrimEnd('/') + "/v1/" + signal);
            exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
            exporter.TimeoutMilliseconds = options.ExportTimeoutMilliseconds;
            exporter.Headers = null; // Credentials belong exclusively to the collector.
        }
        builder.Services.AddOpenTelemetry()
            .WithTracing(traces => traces.SetResourceBuilder(resource).AddSource(ApplicationTelemetry.Name)
                .SetSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio))
                .AddOtlpExporter(exporter =>
                {
                    Export(exporter, "traces");
                    exporter.BatchExportProcessorOptions = new()
                    { MaxQueueSize = 512, MaxExportBatchSize = 128, ScheduledDelayMilliseconds = 5000, ExporterTimeoutMilliseconds = options.ExportTimeoutMilliseconds };
                }))
            .WithMetrics(metrics => metrics.SetResourceBuilder(resource).AddMeter(ApplicationTelemetry.Name, "System.Runtime")
                .AddView(instrument => instrument.Name switch
                {
                    "ihb.http.request.duration" => new ExplicitBucketHistogramConfiguration
                    { Boundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 30, 60, 300], CardinalityLimit = 512 },
                    "ihb.dependency.duration" => new ExplicitBucketHistogramConfiguration
                    { Boundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 30, 120], CardinalityLimit = 128 },
                    "ihb.database.duration" => new ExplicitBucketHistogramConfiguration
                    { Boundaries = [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 30], CardinalityLimit = 16 },
                    _ => new MetricStreamConfiguration { CardinalityLimit = 64 }
                })
                .AddOtlpExporter((exporter, reader) =>
                {
                    Export(exporter, "metrics");
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.MetricIntervalMilliseconds;
                    reader.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = options.ExportTimeoutMilliseconds;
                }));
        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>((category, level) => category == "IHaveBeen.Operations" && level >= LogLevel.Warning);
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resource);
            logging.IncludeScopes = false;
            logging.IncludeFormattedMessage = false;
            logging.AddOtlpExporter((exporter, processor) =>
            {
                Export(exporter, "logs");
                processor.BatchExportProcessorOptions.MaxQueueSize = 256;
                processor.BatchExportProcessorOptions.MaxExportBatchSize = 64;
                processor.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = options.ExportTimeoutMilliseconds;
            });
        });
        builder.Services.AddHostedService<DependencyHealthWorker>();
        return builder;
    }
}
