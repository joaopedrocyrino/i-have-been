using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;
using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.Experiences;

namespace IHaveBeen.Application.Experiences;

public sealed record UpdateExperience(Guid LogId, Guid Id, ExperienceDetails Details);
public sealed class UpdateExperienceHandler(IExperienceRepository repository, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<ExperienceDto>> HandleAsync(UpdateExperience command, CancellationToken cancellationToken)
    {
        var experience = await repository.GetOwnedAsync(command.Id, user.RequireUserId(), cancellationToken);
        if (experience is null || experience.TravelLogId != command.LogId) return Result<ExperienceDto>.NotFound();
        try { experience.Update(command.Details, clock.GetUtcNow()); }
        catch (DomainRuleException ex) { return Result<ExperienceDto>.Invalid(ex.Message); }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ExperienceDto>.Success(ExperienceDto.From(experience));
    }
}
