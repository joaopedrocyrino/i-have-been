using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Domain.TravelLogs;
using Microsoft.EntityFrameworkCore;

namespace IHaveBeen.Infrastructure.Persistence.Repositories;

internal sealed class TravelLogRepository(AppDbContext db) : ITravelLogRepository
{
    public async Task<IReadOnlyList<TravelLog>> ListOwnedAsync(string ownerId, CancellationToken ct) =>
        await db.TravelLogs.AsNoTracking().Include(x => x.Media).Include(x => x.Experiences).AsSplitQuery().Where(x => x.OwnerId == ownerId).OrderByDescending(x => x.VisitedOn).ToListAsync(ct);
    public async Task<IReadOnlyList<TravelLog>> ListSharedAsync(string ownerId, CancellationToken ct) =>
        await db.TravelLogs.AsNoTracking().Include(x => x.Media).Include(x => x.Experiences).AsSplitQuery().Where(x => x.OwnerId == ownerId && x.IncludeInShares).OrderByDescending(x => x.VisitedOn).ToListAsync(ct);
    public Task<TravelLog?> GetOwnedAsync(Guid id, string ownerId, CancellationToken ct) =>
        db.TravelLogs.Include(x => x.Media).Include(x => x.Experiences).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, ct);
    public Task<bool> ExistsOwnedAsync(Guid id, string ownerId, CancellationToken ct) => db.TravelLogs.AnyAsync(x => x.Id == id && x.OwnerId == ownerId, ct);
    public void Add(TravelLog log) => db.TravelLogs.Add(log);
    public void Remove(TravelLog log) => db.TravelLogs.Remove(log);
}
