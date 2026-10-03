using IHaveBeen.Application.Common;
using IHaveBeen.Application.Media;
using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Web.Common;
using IHaveBeen.Web.Features.TravelLogs;

namespace IHaveBeen.Web.Features.Media;

internal static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").RequireAuthorization().WithTags("Media");
        group.MapGet("/media/usage", async (GetMediaUsageHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(ct)).ToHttpResult(usage => Results.Ok(usage)));
        group.MapPost("/logs/{id:guid}/media", UploadAsync).DisableAntiforgery().RequireRateLimiting("uploads")
            .WithRequestTimeout("uploads"); // Common middleware validates before multipart parsing.
        group.MapDelete("/media/{id:guid}", async (Guid id, DeleteMediaHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(id, ct)).ToHttpResult(_ => Results.NoContent()));
        group.MapGet("/media/{id:guid}", async (Guid id, HttpContext context, GetOwnedMediaHandler handler, MediaContentReader reader, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.IsSuccess ? await MediaResponseWriter.WriteAsync(result.Value!, context, reader, ct) : result.Error!.ToHttpResult();
        });
        return routes;
    }
    private static async Task<IResult> UploadAsync(Guid id, HttpContext context, UploadMediaHandler handler, CancellationToken ct)
    {
        if (!context.Request.HasFormContentType) return Result<MediaDto>.Invalid("Upload a multipart file.").Error!.ToHttpResult();
        IFormCollection form;
        try { form = await context.Request.ReadFormAsync(ct); }
        catch (InvalidDataException) { return Result<MediaDto>.Invalid("The multipart upload exceeded a limit or was malformed.").Error!.ToHttpResult(); }
        if (form.Files.Count != 1) return Result<MediaDto>.Invalid("Upload one file per request.").Error!.ToHttpResult();
        var file = form.Files[0];
        await using var stream = file.OpenReadStream();
        var result = await handler.HandleAsync(new(id, stream, file.FileName, file.ContentType, file.Length, form["caption"].ToString()), ct);
        return result.ToHttpResult(media => Results.Created($"/api/media/{media.Id}", MediaView.From(media)));
    }
}
