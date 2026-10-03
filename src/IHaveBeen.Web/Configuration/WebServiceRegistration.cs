using System.Net;
using IHaveBeen.Web.Common;
using IHaveBeen.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

namespace IHaveBeen.Web.Configuration;

internal static class WebServiceRegistration
{
    public static WebApplicationBuilder AddPresentation(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var environment = builder.Environment;
        var secureCookies = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "ihb.account"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = secureCookies;
            // Persistence is opt-in at sign-in; both ticket types have this absolute maximum.
            o.ExpireTimeSpan = TimeSpan.FromDays(15); o.SlidingExpiration = false; o.LoginPath = "/login";
            o.Events.OnRedirectToLogin = c => { if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 401; else c.Response.Redirect("/login"); return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        services.AddAuthorization();
        services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "ihb.csrf"; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = secureCookies;
        });
        services.AddCascadingAuthenticationState();
        services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = environment.IsDevelopment())
            .AddHubOptions(o => o.MaximumReceiveMessageSize = 4 * 1024 * 1024);
        services.Configure<FormOptions>(o =>
        {
            o.MultipartBodyLengthLimit = 100 * 1024 * 1024;
            o.ValueCountLimit = 16; o.ValueLengthLimit = 16384; o.KeyLengthLimit = 128;
            o.MultipartHeadersCountLimit = 16; o.MultipartHeadersLengthLimit = 8192;
        });
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 101 * 1024 * 1024);
        services.AddDataProtection().SetApplicationName("i-have-been")
            .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:Path"] ?? "/var/lib/i-have-been/keys"));
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in builder.Configuration.GetSection("Security:TrustedProxies").Get<string[]>() ?? []) o.KnownProxies.Add(IPAddress.Parse(proxy));
        });
        services.AddScoped<ShareSessionCookies>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(o => o.CustomizeProblemDetails = context => context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddRequestLimits(builder.Configuration);
        return builder;
    }
}
