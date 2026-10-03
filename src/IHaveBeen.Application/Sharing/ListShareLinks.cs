using IHaveBeen.Application.Abstractions;

namespace IHaveBeen.Application.Sharing;

public sealed record ShareLinkDto(Guid Id, string Label, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt);
public sealed class ListShareLinksHandler(IShareLinkRepository repository, ICurrentUser user)
{
    public async Task<IReadOnlyList<ShareLinkDto>> HandleAsync(CancellationToken cancellationToken) =>
        (await repository.ListOwnedAsync(user.RequireUserId(), cancellationToken)).Select(s => new ShareLinkDto(s.Id, s.Label, s.ExpiresAt, s.RevokedAt)).ToList();
}
