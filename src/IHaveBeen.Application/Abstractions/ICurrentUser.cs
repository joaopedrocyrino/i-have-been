namespace IHaveBeen.Application.Abstractions;

public interface ICurrentUser { string? UserId { get; } }
public static class CurrentUserExtensions
{
    public static string RequireUserId(this ICurrentUser user) => user.UserId ?? throw new UnauthorizedAccessException();
}
