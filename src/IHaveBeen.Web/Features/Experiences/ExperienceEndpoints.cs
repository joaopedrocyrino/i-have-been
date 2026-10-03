using IHaveBeen.Application.Experiences;
using IHaveBeen.Web.Common;

namespace IHaveBeen.Web.Features.Experiences;

internal static class ExperienceEndpoints
{
    public static IEndpointRouteBuilder MapExperienceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/logs/{logId:guid}/experiences").RequireAuthorization().WithTags("City experiences");
        group.MapPost("", async (Guid logId, ExperienceInput input, CreateExperienceHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(new(logId, input.ToDetails()), ct)).ToHttpResult(experience => Results.Created($"/api/logs/{logId}/experiences/{experience.Id}", ExperienceView.From(experience))));
        group.MapPut("/{id:guid}", async (Guid logId, Guid id, ExperienceInput input, UpdateExperienceHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(new(logId, id, input.ToDetails()), ct)).ToHttpResult(experience => Results.Ok(ExperienceView.From(experience))));
        group.MapDelete("/{id:guid}", async (Guid logId, Guid id, DeleteExperienceHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(logId, id, ct)).ToHttpResult(_ => Results.NoContent()));
        return routes;
    }
}
