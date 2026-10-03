using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.Experiences;

public sealed class DeleteExperienceHandler(IExperienceRepository repository, IUnitOfWork unitOfWork, ICurrentUser user)
{
    public async Task<Result<Unit>> HandleAsync(Guid logId, Guid id, CancellationToken cancellationToken)
    {
        var experience = await repository.GetOwnedAsync(id, user.RequireUserId(), cancellationToken);
        if (experience is null || experience.TravelLogId != logId) return Result<Unit>.NotFound();
        repository.Remove(experience);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(default);
    }
}
