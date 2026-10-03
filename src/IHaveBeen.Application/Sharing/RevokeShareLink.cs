using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.Sharing;

public sealed class RevokeShareLinkHandler(IShareLinkRepository repository, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var link = await repository.GetOwnedAsync(id, user.RequireUserId(), cancellationToken);
        if (link is null) return Result<Unit>.NotFound();
        link.Revoke(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(default);
    }
}
