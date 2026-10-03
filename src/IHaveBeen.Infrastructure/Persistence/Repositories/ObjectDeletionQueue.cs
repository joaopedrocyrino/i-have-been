using IHaveBeen.Application.Abstractions;
using IHaveBeen.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using IHaveBeen.Application.Media;

namespace IHaveBeen.Infrastructure.Persistence.Repositories;

internal sealed class ObjectDeletionQueue(AppDbContext db) : IObjectDeletionQueue
{
    public Guid Enqueue(string key, DateTimeOffset now, DateTimeOffset? notBefore = null)
    {
        var pending = new PendingObjectDeletion { ObjectKey = key, CreatedAt = now, NotBefore = notBefore ?? now };
        db.PendingObjectDeletions.Add(pending);
        return pending.Id;
    }
    public async Task RemoveAsync(Guid id, CancellationToken ct)
    {
        var pending = await db.PendingObjectDeletions.FindAsync([id], ct);
        if (pending is not null) db.PendingObjectDeletions.Remove(pending);
    }
    public async Task<IReadOnlyList<ObjectDeletion>> GetDueAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await db.PendingObjectDeletions.Where(x => x.NotBefore <= now).OrderBy(x => x.CreatedAt).Take(limit).Select(x => new ObjectDeletion(x.Id, x.ObjectKey)).ToListAsync(ct);
}
internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.CheckViolation, ConstraintName: "CK_MediaAssets_UserQuota" })
        { throw new MediaQuotaExceededException(); }
    }
}
