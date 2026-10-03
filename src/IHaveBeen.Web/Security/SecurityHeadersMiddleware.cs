namespace IHaveBeen.Web.Security;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["X-Frame-Options"] = "DENY";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cache-Control"] = "no-store, private";
            var localMedia = context.Request.Path.StartsWithSegments("/offline") ? " blob:" : "";
            headers["Content-Security-Policy"] = $"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:{localMedia} https://tile.openstreetmap.org https://*.tile.openstreetmap.org; media-src 'self'{localMedia}; connect-src 'self'{localMedia} ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
            return Task.CompletedTask;
        });
        if (!environment.IsDevelopment() && !context.Request.IsHttps) { context.Response.StatusCode = 503; return; }
        await next(context);
    }
}
