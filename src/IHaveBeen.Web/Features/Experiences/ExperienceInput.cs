using System.ComponentModel.DataAnnotations;
using IHaveBeen.Domain.Experiences;

namespace IHaveBeen.Web.Features.Experiences;

public sealed class ExperienceInput
{
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [StringLength(10000)] public string Description { get; set; } = "";
    public ExperienceCategory Category { get; set; } = ExperienceCategory.Restaurant;
    [Range(0, 5)] public int Rating { get; set; }
    public DateOnly? VisitedOn { get; set; }
    [StringLength(300)] public string Address { get; set; } = "";
    public ExperienceDetails ToDetails() => new(Title, Description, Category, Rating, VisitedOn, Address);
}
