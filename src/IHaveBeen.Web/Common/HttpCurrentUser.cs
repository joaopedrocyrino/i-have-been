using System.Security.Claims;
using IHaveBeen.Application.Abstractions;

namespace IHaveBeen.Web.Common;

internal sealed class HttpCurrentUser(IHttpContextAccessor context) : ICurrentUser
{
    public string? UserId => context.HttpContext?.User.Identity?.IsAuthenticated == true
        ? context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
