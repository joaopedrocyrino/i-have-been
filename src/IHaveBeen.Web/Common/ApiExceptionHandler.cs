using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;

namespace IHaveBeen.Web.Common;

internal sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (context.Response.HasStarted) return false;
        context.Response.StatusCode = exception switch
        {
            BadHttpRequestException bad => bad.StatusCode,
            AntiforgeryValidationException => 400,
            UnauthorizedAccessException => 401,
            _ => 500
        };
        if (context.Response.StatusCode == 500) logger.LogError("Request failed ({ExceptionType}).", exception.GetType().Name);
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new()
            {
                Status = context.Response.StatusCode,
                Detail = context.Response.StatusCode == 500 ? "The request could not be completed." : "The request was not accepted."
            }
        });
        return true;
    }
}
