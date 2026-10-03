using IHaveBeen.Domain.Experiences;

namespace IHaveBeen.Application.Experiences;

public interface IExperienceRepository
{
    Task<Experience?> GetOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    void Add(Experience experience);
    void Remove(Experience experience);
}
