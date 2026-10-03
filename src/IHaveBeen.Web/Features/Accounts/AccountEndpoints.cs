using IHaveBeen.Application.Accounts;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Web.Features.Accounts;

internal static class AccountEndpoints
{
    private static bool IsTrustedDevice(IFormCollection form) =>
        bool.TryParse(form["trustDevice"].ToString(), out var trusted) && trusted;

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/account/login", async (HttpContext context, LoginAccountHandler handler) =>
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var signedIn = await handler.HandleAsync(new(form["email"].ToString(), form["password"].ToString(), IsTrustedDevice(form)));
            return Results.Redirect(signedIn ? "/" : "/login?error=1");
        }).AllowAnonymous().RequireRateLimiting("sensitive");
        routes.MapPost("/account/register", async (HttpContext context, RegisterAccountHandler handler) =>
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var result = await handler.HandleAsync(new(form["name"].ToString(), form["email"].ToString(), form["password"].ToString(), IsTrustedDevice(form)));
            return result.Error?.Kind == ErrorKind.Forbidden ? Results.StatusCode(403) : Results.Redirect(result.IsSuccess ? "/" : "/register?error=1");
        }).AllowAnonymous().RequireRateLimiting("sensitive");
        routes.MapPost("/account/logout", async (LogoutAccountHandler handler) => { await handler.HandleAsync(); return Results.Redirect("/login"); }).RequireAuthorization();
        routes.MapGet("/api/me", async (GetMyProfileHandler handler, CancellationToken ct) => Results.Ok(await handler.HandleAsync(ct))).RequireAuthorization();
        return routes;
    }
}
