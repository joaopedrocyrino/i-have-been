using System.ComponentModel.DataAnnotations;

namespace IHaveBeen.Web.Observability;

internal sealed class TelemetryOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://otel-collector:4318";
    [Range(0, 1)] public double TraceSampleRatio { get; set; } = 0.1;
    [Range(10000, 300000)] public int MetricIntervalMilliseconds { get; set; } = 60000;
    [Range(500, 10000)] public int ExportTimeoutMilliseconds { get; set; } = 3000;
    public string ServiceVersion { get; set; } = "local";

    public void Validate()
    {
        Validator.ValidateObject(this, new ValidationContext(this), true);
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("Telemetry endpoint must be an HTTP(S) collector URL without credentials, query or fragment.");
        if (ServiceVersion.Length is < 1 or > 64 || ServiceVersion.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')))
            throw new InvalidOperationException("Telemetry service version must be a short version or commit identifier.");
    }
}
