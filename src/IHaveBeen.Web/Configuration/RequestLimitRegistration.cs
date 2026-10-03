using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using IHaveBeen.Web.Security;
using Microsoft.AspNetCore.Http.Timeouts;

namespace IHaveBeen.Web.Configuration;

internal static class RequestLimitRegistration
{
    public static IServiceCollection AddRequestLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection("RequestLimits").Get<RequestLimitOptions>() ?? new();
        Validator.ValidateObject(limits, new ValidationContext(limits), true);
        services.AddSingleton(_ => new IpRequestLimiter(limits));
        services.AddRequestTimeouts(options => options.AddPolicy("uploads", new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(limits.UploadTimeoutSeconds),
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = context => Results.Problem(statusCode: 504,
                detail: "The upload took too long. Reload your journal before trying again.").ExecuteAsync(context)
        }));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after)
                    ? Math.Max(1, (int)Math.Ceiling(after.TotalSeconds)) : 1;
                context.HttpContext.Response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: 429, detail: "Too many requests. Please wait before trying again.")
                    .ExecuteAsync(context.HttpContext);
            };
            // WebSocket lifetimes must not consume HTTP concurrency permits. Their
            // handshakes/polls still share IP and account request budgets.
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(c => IsWebSocket(c)
                    ? RateLimitPartition.GetConcurrencyLimiter("connections", _ => new() { PermitLimit = limits.ConcurrentConnections, QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter<string>("http")),
                PartitionedRateLimiter.Create<HttpContext, string>(c => IsWebSocket(c)
                    ? RateLimitPartition.GetConcurrencyLimiter(Account(c), _ => new() { PermitLimit = limits.ConcurrentConnectionsPerAccount, QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter<string>("http")),
                PartitionedRateLimiter.Create<HttpContext, string>(c => c.User.FindFirstValue(ClaimTypes.NameIdentifier) is {} id
                    ? Window("user:" + id, limits.AccountPerMinute) : RateLimitPartition.GetNoLimiter<string>("anonymous")),
                PartitionedRateLimiter.Create<HttpContext, string>(c => IsUpload(c)
                    ? RateLimitPartition.GetConcurrencyLimiter("uploads", _ => new() { PermitLimit = limits.ConcurrentUploads, QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter<string>("other")),
                PartitionedRateLimiter.Create<HttpContext, string>(c => IsUpload(c)
                    ? RateLimitPartition.GetConcurrencyLimiter(Account(c), _ => new() { PermitLimit = limits.ConcurrentUploadsPerAccount, QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter<string>("other")));
            options.AddPolicy("sensitive", c => Window(Ip(c), limits.SensitivePerMinute));
            options.AddPolicy("uploads", c => Window(Account(c), limits.UploadsPerMinute));
        });
        return services;
    }

    private static bool IsUpload(HttpContext c) => HttpMethods.IsPost(c.Request.Method)
        && c.Request.Path.StartsWithSegments("/api/logs") && c.Request.Path.Value!.EndsWith("/media", StringComparison.Ordinal);
    private static bool IsWebSocket(HttpContext c) => c.Request.Path.StartsWithSegments("/_blazor") && c.WebSockets.IsWebSocketRequest;
    private static string Ip(HttpContext c) => "ip:" + (c.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    private static string Account(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier) is {} id ? "user:" + id : Ip(c);
    private static RateLimitPartition<string> Window(string key, int count) => RateLimitPartition.GetFixedWindowLimiter(key,
        _ => new() { PermitLimit = count, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
}
