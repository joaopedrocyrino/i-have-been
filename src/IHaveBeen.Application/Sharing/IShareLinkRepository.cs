using IHaveBeen.Domain.Sharing;

namespace IHaveBeen.Application.Sharing;

public interface IShareLinkRepository
{
    Task<IReadOnlyList<ShareLink>> ListOwnedAsync(string ownerId, CancellationToken cancellationToken);
    Task<ShareLink?> GetOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<ShareLink?> GetActiveAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(ShareLink link);
}
