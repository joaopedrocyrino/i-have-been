using IHaveBeen.Application.Experiences;
using IHaveBeen.Application.Media;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.Application.TravelLogs;

public sealed record TravelLogDto(Guid Id, string Title, string Description, string City, string Country,
    double Latitude, double Longitude, DateOnly? VisitedOn, DateOnly? EndedOn, bool IncludeInShares, IReadOnlyList<MediaDto> Media, bool IsWishlist, DateOnly? PlannedOn, IReadOnlyList<ExperienceDto> Experiences)
{
    public static TravelLogDto From(TravelLog log) => new(log.Id, log.Title, log.Description, log.City, log.Country,
        log.Latitude, log.Longitude, log.VisitedOn, log.EndedOn, log.IncludeInShares,
        log.Media.OrderBy(m => m.SortOrder).Select(MediaDto.From).ToList(), log.Status == TravelLogStatus.Wishlist, log.PlannedOn,
        log.Experiences.OrderByDescending(x => x.VisitedOn).ThenBy(x => x.CreatedAt).Select(ExperienceDto.From).ToList());
}
