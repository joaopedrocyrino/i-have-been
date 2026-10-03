using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;

namespace IHaveBeen.Web.Security;

internal sealed class RequestSecurityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api") || request.Path.StartsWithSegments("/account"))
        {
            if (!request.Path.Value!.EndsWith("/media", StringComparison.Ordinal))
            {
                var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (limit is not null && !limit.IsReadOnly) limit.MaxRequestBodySize = 65536;
            }
            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)) await antiforgery.ValidateRequestAsync(context);
        }
        await next(context);
    }
}
