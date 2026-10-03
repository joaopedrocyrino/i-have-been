using IHaveBeen.Domain.Media;

namespace IHaveBeen.Application.Media;

public interface IMediaRepository
{
    Task<MediaAsset?> GetOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<MediaAsset?> GetSharedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<int> CountForLogAsync(Guid logId, CancellationToken cancellationToken);
    void Add(MediaAsset media);
    void Remove(MediaAsset media);
}
