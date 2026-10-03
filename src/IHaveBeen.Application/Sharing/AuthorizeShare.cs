using IHaveBeen.Domain.Sharing;

namespace IHaveBeen.Application.Sharing;

public sealed record ShareCredential(Guid Id, string? TokenHash);
public sealed class AuthorizeShareHandler(IShareLinkRepository repository, IShareTokenService tokens, TimeProvider clock)
{
    public async Task<ShareLink?> HandleAsync(ShareCredential credential, CancellationToken cancellationToken)
    {
        if (credential.TokenHash is null) return null;
        var link = await repository.GetActiveAsync(credential.Id, clock.GetUtcNow(), cancellationToken);
        return link is not null && link.IsActive(clock.GetUtcNow()) && tokens.Matches(link.TokenHash, credential.TokenHash) ? link : null;
    }
}
