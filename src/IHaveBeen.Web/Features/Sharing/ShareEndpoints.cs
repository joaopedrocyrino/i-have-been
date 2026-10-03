using IHaveBeen.Application.Media;
using IHaveBeen.Application.Sharing;
using IHaveBeen.Web.Common;
using IHaveBeen.Web.Features.Media;
using IHaveBeen.Web.Features.TravelLogs;
using IHaveBeen.Web.Security;

namespace IHaveBeen.Web.Features.Sharing;

internal static class ShareEndpoints
{
    public static IEndpointRouteBuilder MapShareEndpoints(this IEndpointRouteBuilder routes)
    {
        var owner = routes.MapGroup("/api/shares").RequireAuthorization().WithTags("Sharing");
    
        owner.MapGet("", async (ListShareLinksHandler handler, CancellationToken ct) => Results.Ok(await handler.HandleAsync(ct)));
        owner.MapPost("", async (ShareInput input, CreateShareLinkHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(new(input.Label, input.ExpiresAt), ct)).ToHttpResult(link => Results.Ok(new { link.Id, path = $"/s/{link.Id}#{link.Token}" })))
            .RequireRateLimiting("sensitive");
        owner.MapDelete("/{id:guid}", async (Guid id, RevokeShareLinkHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(id, ct)).ToHttpResult(_ => Results.NoContent()));

        var shared = routes.MapGroup("/api/shared/{id:guid}").AllowAnonymous().WithTags("Shared journal");
        shared.MapPost("/session", async (Guid id, ExchangeInput input, HttpContext context, ExchangeShareTokenHandler handler, ShareSessionCookies cookies, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, input.Token, ct);
            if (!result.IsSuccess) return result.Error!.ToHttpResult();
            cookies.Write(context, result.Value!);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");
        shared.MapGet("/logs", async (Guid id, HttpContext context, ShareSessionCookies cookies, GetSharedJournalHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(cookies.Read(context, id), ct)).ToHttpResult(journal => Results.Ok(new
            {
                journal.Name,
                journal.ExpiresAt,
                logs = journal.Logs.Select(log => LogView.From(log, $"/api/shared/{id}/media"))
            })));
        shared.MapGet("/media/{mediaId:guid}", async (Guid id, Guid mediaId, HttpContext context, ShareSessionCookies cookies,
            GetSharedMediaHandler handler, MediaContentReader reader, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(cookies.Read(context, id), mediaId, ct);
            return result.IsSuccess ? await MediaResponseWriter.WriteAsync(result.Value!, context, reader, ct) : result.Error!.ToHttpResult();
        });
        return routes;
    }
}
