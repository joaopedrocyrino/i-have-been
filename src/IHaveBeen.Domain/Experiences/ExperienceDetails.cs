using IHaveBeen.Domain.Common;

namespace IHaveBeen.Domain.Experiences;

public sealed record ExperienceDetails(string Title, string Description, ExperienceCategory Category, int Rating,
    DateOnly? VisitedOn, string Address)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > 160 || Description is null || Description.Length > 10000
            || !Enum.IsDefined(Category) || Rating is < 0 or > 5 || Address is null || Address.Length > 300 || VisitedOn == default(DateOnly))
            throw new DomainRuleException("Choose a title, valid category and a rating between 0 and 5.");
    }
}
