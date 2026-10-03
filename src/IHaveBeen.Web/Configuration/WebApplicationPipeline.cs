using IHaveBeen.Web.Features.Offline;
using IHaveBeen.Web.Features.Accounts;
using IHaveBeen.Web.Features.Experiences;
using IHaveBeen.Web.Features.Media;
using IHaveBeen.Web.Features.Sharing;
using IHaveBeen.Web.Features.TravelLogs;
using IHaveBeen.Web.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IHaveBeen.Web.Configuration;

internal static class WebApplicationPipeline
{
    public static WebApplication UsePresentation(this WebApplication app)
    {
        app.UseForwardedHeaders();
        if (app.Services.GetRequiredService<IHaveBeen.Web.Observability.TelemetryOptions>().Enabled)
            app.UseMiddleware<IHaveBeen.Web.Observability.RequestTelemetryMiddleware>();
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseRouting();
        app.UseWebSockets();
        app.UseMiddleware<IpRequestLimitMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseRequestTimeouts();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.UseMiddleware<RequestSecurityMiddleware>();
        return app;
    }
    public static WebApplication MapFeatures(this WebApplication app)
    {
        app.MapGet("/api/csrf", (HttpContext c, IAntiforgery af) => Results.Ok(new { token = af.GetAndStoreTokens(c).RequestToken })).AllowAnonymous();
        app.MapGet("/health/live", () => Results.Ok(new { status = "up" })).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new { status = report.Status == HealthStatus.Healthy ? "ready" : "unavailable" })
        }).AllowAnonymous();
        app.MapOfflineEndpoints(app.Environment.WebRootFileProvider);
        app.MapAccountEndpoints();
        app.MapTravelLogEndpoints();
        app.MapExperienceEndpoints();
        app.MapMediaEndpoints();
        app.MapShareEndpoints();
        return app;
    }
}
