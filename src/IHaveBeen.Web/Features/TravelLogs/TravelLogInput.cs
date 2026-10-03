using System.ComponentModel.DataAnnotations;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.Web.Features.TravelLogs;

public sealed class LogInput : IValidatableObject
{
    [Required, StringLength(160, MinimumLength = 1)] public string Title { get; set; } = "";
    [StringLength(10000)] public string Description { get; set; } = "";
    [StringLength(120)] public string City { get; set; } = "";
    [Required, StringLength(120)] public string Country { get; set; } = "";
    [Range(-90, 90)] public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }
    public DateOnly? VisitedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? EndedOn { get; set; }
    public bool IsWishlist { get; set; }
    public DateOnly? PlannedOn { get; set; }
    public bool IncludeInShares { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsWishlist && string.IsNullOrWhiteSpace(City)) yield return new("City is required for a visited place.", [nameof(City)]);
        if (!IsWishlist && VisitedOn is null) yield return new("A visited date is required.", [nameof(VisitedOn)]);
    }
    public TravelLogDetails ToDetails() => new(Title, Description, City, Country, Latitude, Longitude, VisitedOn, EndedOn, IncludeInShares, IsWishlist ? TravelLogStatus.Wishlist : TravelLogStatus.Visited, PlannedOn);
}
