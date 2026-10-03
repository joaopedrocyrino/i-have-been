using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.Domain.Experiences;

public sealed class Experience
{
    private Experience() { }
    public Guid Id { get; private set; }
    public Guid TravelLogId { get; private set; }
    public TravelLog TravelLog { get; private set; } = null!;
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public ExperienceCategory Category { get; private set; }
    public int Rating { get; private set; }
    public DateOnly? VisitedOn { get; private set; }
    public string Address { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Experience Create(Guid logId, ExperienceDetails details, DateTimeOffset now)
    {
        if (logId == Guid.Empty) throw new ArgumentException("A parent memory is required.", nameof(logId));
        var experience = new Experience { Id = Guid.NewGuid(), TravelLogId = logId, CreatedAt = now };
        experience.Update(details, now);
        return experience;
    }
    public void Update(ExperienceDetails details, DateTimeOffset now)
    {
        details.Validate();
        Title = details.Title.Trim(); Description = details.Description; Category = details.Category;
        Rating = details.Rating; VisitedOn = details.VisitedOn; Address = details.Address.Trim(); UpdatedAt = now;
    }
}
