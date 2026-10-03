using System.Threading.RateLimiting;
using IHaveBeen.Web.Configuration;

namespace IHaveBeen.Web.Security;

internal sealed class IpRequestLimiter(RequestLimitOptions options) : IDisposable
{
    public PartitionedRateLimiter<HttpContext> Limiter { get; } = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(c => c.WebSockets.IsWebSocketRequest && c.Request.Path.StartsWithSegments("/_blazor")
            ? RateLimitPartition.GetNoLimiter<string>("websocket")
            : RateLimitPartition.GetConcurrencyLimiter("http", _ => new() { PermitLimit = options.ConcurrentRequests, QueueLimit = 0 })),
        PartitionedRateLimiter.Create<HttpContext, string>(c =>
            RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new() { PermitLimit = options.IpPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
    public void Dispose() => Limiter.Dispose();
}

internal sealed class IpRequestLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IpRequestLimiter limiter)
    {
        // Reject abusive traffic before cookie validation or database access.
        using var lease = limiter.Limiter.AttemptAcquire(context);
        if (!lease.IsAcquired)
        {
            context.Response.Headers.RetryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out _) ? "60" : "1";
            await Results.Problem(statusCode: 429, detail: "Too many requests. Please wait before trying again.").ExecuteAsync(context);
            return;
        }
        await next(context);
    }
}
