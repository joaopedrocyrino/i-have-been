using IHaveBeen.Domain.Experiences;

namespace IHaveBeen.Application.Experiences;

public sealed record ExperienceDto(Guid Id, string Title, string Description, ExperienceCategory Category, int Rating, DateOnly? VisitedOn, string Address)
{
    public static ExperienceDto From(Experience experience) => new(experience.Id, experience.Title, experience.Description,
        experience.Category, experience.Rating, experience.VisitedOn, experience.Address);
}
