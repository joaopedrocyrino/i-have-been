using IHaveBeen.Domain.Common;

namespace IHaveBeen.Domain.TravelLogs;

public sealed record TravelLogDetails(string Title, string Description, string City, string Country,
    double Latitude, double Longitude, DateOnly? VisitedOn, DateOnly? EndedOn, bool IncludeInShares, TravelLogStatus Status = TravelLogStatus.Visited, DateOnly? PlannedOn = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > 160 || Description is null || Description.Length > 10000
            || City is null || City.Length > 120 || (Status == TravelLogStatus.Visited && string.IsNullOrWhiteSpace(City)) || string.IsNullOrWhiteSpace(Country) || Country.Length > 120
            || !double.IsFinite(Latitude) || Latitude is < -90 or > 90 || !double.IsFinite(Longitude) || Longitude is < -180 or > 180
            || PlannedOn == default(DateOnly) || VisitedOn == default(DateOnly) || EndedOn == default(DateOnly)
            || !Enum.IsDefined(Status) || (Status == TravelLogStatus.Visited && (VisitedOn is null || VisitedOn == default(DateOnly) || EndedOn < VisitedOn)))
            throw new DomainRuleException("Check the title, city, country, coordinates and dates.");
    }
}
