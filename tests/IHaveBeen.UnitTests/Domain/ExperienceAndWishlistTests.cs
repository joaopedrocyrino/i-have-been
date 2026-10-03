using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.Experiences;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.UnitTests.Domain;

public sealed class ExperienceAndWishlistTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-02T12:00:00Z");
    private static ExperienceDetails Details(int rating) => new("A museum", "A beautiful collection", ExperienceCategory.Museum, rating, null, "Centro");
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void Zero_to_five_star_ratings_are_preserved(int rating) => Assert.Equal(rating, Experience.Create(Guid.NewGuid(), Details(rating), Now).Rating);
    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void Invalid_ratings_do_not_partially_mutate_experiences(int rating)
    {
        var item = Experience.Create(Guid.NewGuid(), Details(4), Now);
        Assert.Throws<DomainRuleException>(() => item.Update(Details(rating) with { Title = "Invalid edit" }, Now.AddDays(1)));
        Assert.Equal("A museum", item.Title); Assert.Equal(4, item.Rating); Assert.Equal(Now, item.UpdatedAt);
    }
    [Fact]
    public void Invalid_category_is_rejected() => Assert.Throws<DomainRuleException>(() => Experience.Create(Guid.NewGuid(), Details(3) with { Category = (ExperienceCategory)99 }, Now));
    [Fact]
    public void Country_wishlist_needs_neither_city_nor_visited_date()
    {
        var log = TravelLog.Create("alice", Fixtures.Details() with { Status = TravelLogStatus.Wishlist, City = "", VisitedOn = null, PlannedOn = new DateOnly(2027, 1, 1) }, Now);
        Assert.Equal(TravelLogStatus.Wishlist, log.Status); Assert.Null(log.VisitedOn); Assert.Equal(new DateOnly(2027, 1, 1), log.PlannedOn);
        Assert.Throws<DomainRuleException>(() => log.AddExperience(Experience.Create(log.Id, Details(3), Now)));
    }
    [Fact]
    public void A_wish_can_become_visited_without_losing_its_identity_or_experiences()
    {
        var log = TravelLog.Create("alice", Fixtures.Details() with { Status = TravelLogStatus.Wishlist, VisitedOn = null, PlannedOn = new DateOnly(2027, 1, 1) }, Now);
        var id = log.Id; log.AddExperience(Experience.Create(id, Details(5), Now));
        log.Update(Fixtures.Details(), Now.AddDays(1));
        Assert.Equal(id, log.Id); Assert.Equal("alice", log.OwnerId); Assert.Equal(TravelLogStatus.Visited, log.Status); Assert.Null(log.PlannedOn); Assert.Single(log.Experiences);
    }
    [Fact]
    public void Visited_place_requires_city_and_visit_date()
    {
        Assert.Throws<DomainRuleException>(() => TravelLog.Create("alice", Fixtures.Details() with { City = "" }, Now));
        Assert.Throws<DomainRuleException>(() => TravelLog.Create("alice", Fixtures.Details() with { VisitedOn = null }, Now));
    }
    [Fact]
    public void Experiences_cannot_be_moved_between_memories_or_attached_twice()
    {
        var log = TravelLog.Create("alice", Fixtures.Details(), Now); var item = Experience.Create(log.Id, Details(4), Now);
        Assert.Throws<DomainRuleException>(() => log.AddExperience(Experience.Create(Guid.NewGuid(), Details(4), Now)));
        log.AddExperience(item); Assert.Throws<DomainRuleException>(() => log.AddExperience(item));
        Assert.Throws<DomainRuleException>(() => log.Update(Fixtures.Details() with { Status = TravelLogStatus.Wishlist, City = "" }, Now));
        Assert.Equal("Rio de Janeiro", log.City);
    }
}
