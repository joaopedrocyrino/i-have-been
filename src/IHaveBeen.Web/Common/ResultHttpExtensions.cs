using IHaveBeen.Application.Common;

namespace IHaveBeen.Web.Common;

internal static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> success) => result.IsSuccess
        ? success(result.Value!) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult(this ApplicationError error) => Results.Problem(detail: error.Message,
        statusCode: error.Kind switch { ErrorKind.Validation => 400, ErrorKind.NotFound => 404, ErrorKind.Forbidden => 403,
            ErrorKind.Conflict => 409, ErrorKind.TooLarge => 413, ErrorKind.Unavailable => 503, _ => 500 });
}
