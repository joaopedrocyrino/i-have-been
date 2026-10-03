using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;
using IHaveBeen.Domain.Accounts;

namespace IHaveBeen.Application.Media;

public sealed class GetMediaUsageHandler(IAccountMediaPolicy accounts, ICurrentUser user, MediaLimits limits)
{
    public async Task<Result<MediaUsage>> HandleAsync(CancellationToken ct)
    {
        var account = await accounts.GetAsync(user.RequireUserId(), ct);
        return account is null ? Result<MediaUsage>.Forbidden() : Result<MediaUsage>.Success(new(
            account.UserType.ToString().ToLowerInvariant(), account.Count,
            account.UserType == UserType.Manager ? null : MediaLimits.UserMediaCount,
            limits.ForFile(account.UserType, "image/jpeg"), limits.ForFile(account.UserType, "video/mp4")));
    }
}
