using IHaveBeen.Application.Media;
using Microsoft.EntityFrameworkCore;

namespace IHaveBeen.Infrastructure.Persistence.Repositories;

internal sealed class AccountMediaPolicy(AppDbContext db) : IAccountMediaPolicy
{
    public Task<AccountMediaUsage?> GetAsync(string ownerId, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(x => x.Id == ownerId).Select(x => new AccountMediaUsage(
            x.UserType, db.MediaAssets.Count(m => m.TravelLog.OwnerId == ownerId))).FirstOrDefaultAsync(ct);
}
