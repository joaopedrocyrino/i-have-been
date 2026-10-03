using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;
using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.Sharing;

namespace IHaveBeen.Application.Sharing;

public sealed record CreateShareLink(string Label, DateTimeOffset? ExpiresAt);
public sealed record CreatedShareLink(Guid Id, string Token);
public sealed class CreateShareLinkHandler(IShareLinkRepository repository, IShareTokenService tokens,
    IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<CreatedShareLink>> HandleAsync(CreateShareLink command, CancellationToken cancellationToken)
    {
        var owner = user.RequireUserId();
        var token = tokens.Generate();
        ShareLink link;
        try { link = ShareLink.Create(owner, command.Label, tokens.Hash(token), command.ExpiresAt, clock.GetUtcNow()); }
        catch (DomainRuleException ex) { return Result<CreatedShareLink>.Invalid(ex.Message); }
        repository.Add(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<CreatedShareLink>.Success(new(link.Id, token));
    }
}
