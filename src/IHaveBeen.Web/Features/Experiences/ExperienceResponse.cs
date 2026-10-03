using IHaveBeen.Application.Experiences;

namespace IHaveBeen.Web.Features.Experiences;

public sealed record ExperienceView(Guid Id, string Title, string Description, string Category, int Rating, DateOnly? VisitedOn, string Address)
{
    public static ExperienceView From(ExperienceDto experience) => new(experience.Id, experience.Title, experience.Description,
        experience.Category.ToString(), experience.Rating, experience.VisitedOn, experience.Address);
}
