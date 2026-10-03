using IHaveBeen.Application.Media;
using IHaveBeen.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace IHaveBeen.Infrastructure.Persistence.Repositories;

internal sealed class MediaRepository(AppDbContext db) : IMediaRepository
{
    public Task<MediaAsset?> GetOwnedAsync(Guid id, string ownerId, CancellationToken ct) =>
        db.MediaAssets.FirstOrDefaultAsync(x => x.Id == id && x.TravelLog.OwnerId == ownerId, ct);
    public Task<MediaAsset?> GetSharedAsync(Guid id, string ownerId, CancellationToken ct) =>
        db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TravelLog.OwnerId == ownerId && x.TravelLog.IncludeInShares, ct);
    public Task<int> CountForLogAsync(Guid logId, CancellationToken ct) => db.MediaAssets.CountAsync(x => x.TravelLogId == logId, ct);
    public void Add(MediaAsset media) => db.MediaAssets.Add(media);
    public void Remove(MediaAsset media) => db.MediaAssets.Remove(media);
}
