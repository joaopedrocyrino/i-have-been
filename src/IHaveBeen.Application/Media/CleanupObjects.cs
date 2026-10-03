using IHaveBeen.Application.Abstractions;

namespace IHaveBeen.Application.Media;

public sealed class CleanupObjectsHandler(IObjectDeletionQueue deletions, IObjectStorage storage, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        foreach (var item in await deletions.GetDueAsync(clock.GetUtcNow(), 50, cancellationToken))
        {
            await storage.DeleteAsync(item.ObjectKey, cancellationToken);
            await deletions.RemoveAsync(item.Id, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
