using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.Application.TravelLogs;

public interface ITravelLogRepository
{
    Task<IReadOnlyList<TravelLog>> ListOwnedAsync(string ownerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TravelLog>> ListSharedAsync(string ownerId, CancellationToken cancellationToken);
    Task<TravelLog?> GetOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<bool> ExistsOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    void Add(TravelLog log);
    void Remove(TravelLog log);
}
