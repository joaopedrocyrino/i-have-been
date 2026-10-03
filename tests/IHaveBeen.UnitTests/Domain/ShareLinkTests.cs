using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.Sharing;

namespace IHaveBeen.UnitTests.Domain;

public sealed class ShareLinkTests
{
    [Fact]
    public void Expiration_is_exclusive_at_the_exact_deadline()
    {
        var clock = new TestClock(); var end = clock.Now.AddHours(1);
        var link = ShareLink.Create("alice", "Friends", new string('a', 64), end, clock.Now);
        Assert.True(link.IsActive(end.AddTicks(-1))); Assert.False(link.IsActive(end)); Assert.False(link.IsActive(end.AddTicks(1)));
    }
    [Fact]
    public void A_non_expiring_link_can_still_be_revoked()
    {
        var clock = new TestClock(); var link = ShareLink.Create("alice", "Friends", new string('a', 64), null, clock.Now);
        Assert.True(link.IsActive(clock.Now.AddYears(50)));
        link.Revoke(clock.Now.AddHours(1)); link.Revoke(clock.Now.AddHours(2));
        Assert.Equal(clock.Now.AddHours(1), link.RevokedAt); Assert.False(link.IsActive(clock.Now.AddHours(2)));
    }
    [Fact]
    public void A_new_invitation_cannot_already_be_expired() => Assert.Throws<DomainRuleException>(() =>
        ShareLink.Create("alice", "Friends", new string('a', 64), new TestClock().Now, new TestClock().Now));
}
