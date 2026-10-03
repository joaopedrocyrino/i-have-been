using IHaveBeen.Application.Experiences;
using IHaveBeen.Domain.Experiences;
using Microsoft.EntityFrameworkCore;

namespace IHaveBeen.Infrastructure.Persistence.Repositories;

internal sealed class ExperienceRepository(AppDbContext db) : IExperienceRepository
{
    public Task<Experience?> GetOwnedAsync(Guid id, string ownerId, CancellationToken ct) =>
        db.Experiences.FirstOrDefaultAsync(x => x.Id == id && x.TravelLog.OwnerId == ownerId, ct);
    public void Add(Experience experience) => db.Experiences.Add(experience);
    public void Remove(Experience experience) => db.Experiences.Remove(experience);
}
