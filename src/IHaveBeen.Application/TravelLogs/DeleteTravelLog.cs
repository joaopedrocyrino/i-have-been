using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.TravelLogs;

public sealed record DeleteTravelLog(Guid Id);
public sealed class DeleteTravelLogHandler(ITravelLogRepository repository, IObjectDeletionQueue deletions,
    IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(DeleteTravelLog command, CancellationToken cancellationToken)
    {
        var log = await repository.GetOwnedAsync(command.Id, user.RequireUserId(), cancellationToken);
        if (log is null) return Result<Unit>.NotFound();
        foreach (var media in log.Media) deletions.Enqueue(media.ObjectKey, clock.GetUtcNow());
        repository.Remove(log);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(default);
    }
}
