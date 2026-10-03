using IHaveBeen.Application.Sharing;
using IHaveBeen.Domain.Sharing;
using Microsoft.EntityFrameworkCore;

namespace IHaveBeen.Infrastructure.Persistence.Repositories;

internal sealed class ShareLinkRepository(AppDbContext db) : IShareLinkRepository
{
    public async Task<IReadOnlyList<ShareLink>> ListOwnedAsync(string ownerId, CancellationToken ct) =>
        await db.ShareLinks.AsNoTracking().Where(x => x.OwnerId == ownerId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    public Task<ShareLink?> GetOwnedAsync(Guid id, string ownerId, CancellationToken ct) =>
        db.ShareLinks.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, ct);
    public Task<ShareLink?> GetActiveAsync(Guid id, DateTimeOffset now, CancellationToken ct) =>
        db.ShareLinks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.RevokedAt == null && (x.ExpiresAt == null || x.ExpiresAt > now), ct);
    public void Add(ShareLink link) => db.ShareLinks.Add(link);
}
