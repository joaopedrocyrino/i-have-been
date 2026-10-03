using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.Experiences;
using IHaveBeen.Domain.Media;

namespace IHaveBeen.Domain.TravelLogs;

public sealed class TravelLog
{
    private readonly List<MediaAsset> media = [];
    private readonly List<Experience> experiences = [];
    private TravelLog() { }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string City { get; private set; } = "";
    public string Country { get; private set; } = "";
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public DateOnly? VisitedOn { get; private set; }
    public DateOnly? EndedOn { get; private set; }
    public TravelLogStatus Status { get; private set; }
    public DateOnly? PlannedOn { get; private set; }
    public bool IncludeInShares { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<MediaAsset> Media => media.AsReadOnly();
    public IReadOnlyCollection<Experience> Experiences => experiences.AsReadOnly();

    public static TravelLog Create(string ownerId, TravelLogDetails details, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) throw new DomainRuleException("An owner is required.");
        var log = new TravelLog { Id = Guid.NewGuid(), OwnerId = ownerId, CreatedAt = now };
        log.Update(details, now);
        return log;
    }

    public void Update(TravelLogDetails details, DateTimeOffset now)
    {
        details.Validate();
        if (experiences.Count > 0 && string.IsNullOrWhiteSpace(details.City)) throw new DomainRuleException("A memory with city experiences must keep its city.");
        Title = details.Title.Trim(); Description = details.Description; City = details.City.Trim(); Country = details.Country.Trim();
        Latitude = details.Latitude; Longitude = details.Longitude; Status = details.Status;
        VisitedOn = Status == TravelLogStatus.Visited ? details.VisitedOn : null;
        EndedOn = Status == TravelLogStatus.Visited ? details.EndedOn : null;
        PlannedOn = Status == TravelLogStatus.Wishlist ? details.PlannedOn : null;
        IncludeInShares = details.IncludeInShares; UpdatedAt = now;
    }
    public void AttachMedia(MediaAsset asset)
    {
        if (asset.TravelLogId != Id || media.Any(item => item.Id == asset.Id))
            throw new DomainRuleException("Media must belong to this memory and cannot be attached twice.");
        media.Add(asset);
    }

    public void AddExperience(Experience experience)
    {
        if (string.IsNullOrWhiteSpace(City)) throw new DomainRuleException("Add a city to this memory before adding city experiences.");
        if (experience.TravelLogId != Id || experiences.Any(item => item.Id == experience.Id))
            throw new DomainRuleException("An experience must belong to this memory and cannot be added twice.");
        experiences.Add(experience);
    }

}
