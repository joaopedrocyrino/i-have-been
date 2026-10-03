using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;
using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.Experiences;

namespace IHaveBeen.Application.Experiences;

public sealed record CreateExperience(Guid LogId, ExperienceDetails Details);
public sealed class CreateExperienceHandler(ITravelLogRepository logs, IExperienceRepository repository, IUnitOfWork unitOfWork,
    ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<ExperienceDto>> HandleAsync(CreateExperience command, CancellationToken cancellationToken)
    {
        var log = await logs.GetOwnedAsync(command.LogId, user.RequireUserId(), cancellationToken);
        if (log is null) return Result<ExperienceDto>.NotFound();
        Experience experience;
        try { experience = Experience.Create(log.Id, command.Details, clock.GetUtcNow()); log.AddExperience(experience); }
        catch (DomainRuleException ex) { return Result<ExperienceDto>.Invalid(ex.Message); }
        repository.Add(experience);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ExperienceDto>.Success(ExperienceDto.From(experience));
    }
}
