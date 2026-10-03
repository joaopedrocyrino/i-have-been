using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Web.Common;

namespace IHaveBeen.Web.Features.TravelLogs;

internal static class TravelLogEndpoints
{
    public static IEndpointRouteBuilder MapTravelLogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/logs").RequireAuthorization().WithTags("Travel logs");
        group.MapGet("", async (ListTravelLogsHandler handler, CancellationToken ct) =>
            Results.Ok((await handler.HandleAsync(ct)).Select(log => LogView.From(log))));
        group.MapPost("", async (LogInput input, CreateTravelLogHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(new(input.ToDetails()), ct)).ToHttpResult(log => Results.Created($"/api/logs/{log.Id}", LogView.From(log))));
        group.MapPut("/{id:guid}", async (Guid id, LogInput input, UpdateTravelLogHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(new(id, input.ToDetails()), ct)).ToHttpResult(log => Results.Ok(LogView.From(log))));
        group.MapDelete("/{id:guid}", async (Guid id, DeleteTravelLogHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(new(id), ct)).ToHttpResult(_ => Results.NoContent()));
        return routes;
    }
}
