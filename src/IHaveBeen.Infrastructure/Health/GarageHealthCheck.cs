using Amazon.S3;
using IHaveBeen.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IHaveBeen.Infrastructure.Health;

internal sealed class GarageHealthCheck(GarageObjectStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try { await storage.CheckAvailabilityAsync(ct); return HealthCheckResult.Healthy(); }
        catch (Exception error) { return HealthCheckResult.Unhealthy(FailureDescription(error)); }
    }

    // Health-check logging records Description. Never attach raw exceptions: S3 errors
    // can contain credentials, signed request details, object keys or internal URLs.
    internal static string FailureDescription(Exception error)
    {
        if (error is AmazonS3Exception s3)
        {
            var reason = s3.ErrorCode switch
            {
                "AccessDenied" => "access-denied",
                "InvalidAccessKeyId" => "invalid-access-key",
                "SignatureDoesNotMatch" => "signature-mismatch",
                "NoSuchBucket" => "missing-bucket",
                "RequestTimeTooSkewed" or "RequestExpired" => "clock-skew",
                "AuthorizationHeaderMalformed" or "InvalidRegion" => "signing-region",
                "ServiceUnavailable" or "InternalError" => "service-unavailable",
                _ => "s3-error"
            };
            return $"Garage S3 readiness failed: HTTP {(int)s3.StatusCode}; reason={reason}.";
        }
        return error switch
        {
            HttpRequestException => "Garage S3 readiness failed: reason=connection-error.",
            OperationCanceledException => "Garage S3 readiness failed: reason=timeout.",
            _ => "Garage S3 readiness failed: reason=unexpected-error."
        };
    }
}
