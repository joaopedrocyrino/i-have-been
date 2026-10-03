using IHaveBeen.Application.Abstractions;

namespace IHaveBeen.Application.TravelLogs;

public sealed class ListTravelLogsHandler(ITravelLogRepository repository, ICurrentUser user)
{
    public async Task<IReadOnlyList<TravelLogDto>> HandleAsync(CancellationToken cancellationToken) =>
        (await repository.ListOwnedAsync(user.RequireUserId(), cancellationToken)).Select(TravelLogDto.From).ToList();
}
