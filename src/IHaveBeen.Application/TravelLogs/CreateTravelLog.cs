using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;
using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.Application.TravelLogs;

public sealed record CreateTravelLog(TravelLogDetails Details);
public sealed class CreateTravelLogHandler(ITravelLogRepository repository, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<TravelLogDto>> HandleAsync(CreateTravelLog command, CancellationToken cancellationToken)
    {
        var owner = user.RequireUserId();
        TravelLog log;
        try { log = TravelLog.Create(owner, command.Details, clock.GetUtcNow()); }
        catch (DomainRuleException ex) { return Result<TravelLogDto>.Invalid(ex.Message); }
        repository.Add(log);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<TravelLogDto>.Success(TravelLogDto.From(log));
    }
}
