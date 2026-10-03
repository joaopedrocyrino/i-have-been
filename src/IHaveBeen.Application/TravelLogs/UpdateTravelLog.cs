using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;
using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.Application.TravelLogs;

public sealed record UpdateTravelLog(Guid Id, TravelLogDetails Details);
public sealed class UpdateTravelLogHandler(ITravelLogRepository repository, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<TravelLogDto>> HandleAsync(UpdateTravelLog command, CancellationToken cancellationToken)
    {
        var log = await repository.GetOwnedAsync(command.Id, user.RequireUserId(), cancellationToken);
        if (log is null) return Result<TravelLogDto>.NotFound();
        try { log.Update(command.Details, clock.GetUtcNow()); }
        catch (DomainRuleException ex) { return Result<TravelLogDto>.Invalid(ex.Message); }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<TravelLogDto>.Success(TravelLogDto.From(log));
    }
}
