using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.Media;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.UnitTests.Domain;

public sealed class TravelLogTests
{
    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void Invalid_coordinates_cannot_enter_the_domain(double lat, double lon) =>
        Assert.Throws<DomainRuleException>(() => TravelLog.Create("alice", Fixtures.Details() with { Latitude = lat, Longitude = lon }, new TestClock().Now));
    [Fact]
    public void End_date_cannot_precede_visit_date() => Assert.Throws<DomainRuleException>(() =>
        TravelLog.Create("alice", Fixtures.Details() with { EndedOn = new DateOnly(2026, 1, 1) }, new TestClock().Now));
    [Fact]
    public void A_memory_requires_an_owner() => Assert.Throws<DomainRuleException>(() => TravelLog.Create("", Fixtures.Details(), new TestClock().Now));
    [Fact]
    public void Invalid_edit_preserves_the_last_valid_state()
    {
        var clock = new TestClock(); var log = TravelLog.Create("alice", Fixtures.Details(), clock.Now);
        Assert.Throws<DomainRuleException>(() => log.Update(Fixtures.Details() with { Title = "", City = "invalid" }, clock.Now.AddMinutes(1)));
        Assert.Equal("Rio", log.Title); Assert.Equal("Rio de Janeiro", log.City); Assert.Equal(clock.Now, log.UpdatedAt);
    }
    [Fact]
    public void An_edit_preserves_ownership_identity_and_creation_time()
    {
        var clock = new TestClock(); var log = TravelLog.Create("alice", Fixtures.Details(), clock.Now); var id = log.Id;
        log.Update(Fixtures.Details(true) with { Title = "  New title  " }, clock.Now.AddMinutes(1));
        Assert.Equal(id, log.Id); Assert.Equal("alice", log.OwnerId); Assert.Equal(clock.Now, log.CreatedAt);
        Assert.Equal("New title", log.Title); Assert.True(log.IncludeInShares); Assert.Equal(clock.Now.AddMinutes(1), log.UpdatedAt);
    }
    [Fact]
    public void Foreign_media_cannot_be_attached_to_a_memory()
    {
        var clock = new TestClock(); var log = TravelLog.Create("alice", Fixtures.Details(), clock.Now);
        var media = MediaAsset.Create(Guid.NewGuid(), Guid.NewGuid(), "private/key", "test.png", "image/png", 10, new string('a', 64), "", 0, clock.Now);
        Assert.Throws<DomainRuleException>(() => log.AttachMedia(media)); Assert.Empty(log.Media);
    }
}
