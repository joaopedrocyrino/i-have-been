using IHaveBeen.Application.Media;
using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Web.Features.Experiences;

namespace IHaveBeen.Web.Features.TravelLogs;

public sealed record MediaView(Guid Id, string OriginalName, string ContentType, long ByteLength, string Sha256, string Caption, int SortOrder, string Url)
{
    public static MediaView From(MediaDto media, string prefix = "/api/media") => new(media.Id, media.OriginalName, media.ContentType,
        media.ByteLength, media.Sha256, media.Caption, media.SortOrder, $"{prefix}/{media.Id}");
}
public sealed record LogView(Guid Id, string Title, string Description, string City, string Country, double Latitude, double Longitude,
    DateOnly? VisitedOn, DateOnly? EndedOn, bool IncludeInShares, List<MediaView> Media, bool IsWishlist, DateOnly? PlannedOn, List<ExperienceView> Experiences)
{
    public static LogView From(TravelLogDto log, string prefix = "/api/media") => new(log.Id, log.Title, log.Description, log.City, log.Country,
        log.Latitude, log.Longitude, log.VisitedOn, log.EndedOn, log.IncludeInShares, log.Media.Select(m => MediaView.From(m, prefix)).ToList(), log.IsWishlist, log.PlannedOn, log.Experiences.Select(ExperienceView.From).ToList());
}
