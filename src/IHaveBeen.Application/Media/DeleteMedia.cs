using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.Media;

public sealed class DeleteMediaHandler(IMediaRepository repository, IObjectDeletionQueue deletions,
    IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await repository.GetOwnedAsync(id, user.RequireUserId(), cancellationToken);
        if (media is null) return Result<Unit>.NotFound();
        deletions.Enqueue(media.ObjectKey, clock.GetUtcNow());
        repository.Remove(media);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(default);
    }
}
