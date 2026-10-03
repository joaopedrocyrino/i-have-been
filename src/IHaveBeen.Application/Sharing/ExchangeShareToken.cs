using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.Sharing;

public sealed record ShareGrant(Guid Id, string TokenHash, DateTimeOffset? ExpiresAt);
public sealed class ExchangeShareTokenHandler(IShareLinkRepository repository, IShareTokenService tokens, TimeProvider clock)
{
    public async Task<Result<ShareGrant>> HandleAsync(Guid id, string? token, CancellationToken cancellationToken)
    {
        if (token is null || token.Length != 43) return Result<ShareGrant>.NotFound();
        var link = await repository.GetActiveAsync(id, clock.GetUtcNow(), cancellationToken);
        if (link is null || !link.IsActive(clock.GetUtcNow()) || !tokens.Matches(link.TokenHash, tokens.Hash(token))) return Result<ShareGrant>.NotFound();
        return Result<ShareGrant>.Success(new(link.Id, link.TokenHash, link.ExpiresAt));
    }
}
