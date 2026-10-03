using IHaveBeen.Application.Common;
using IHaveBeen.Application.Sharing;

namespace IHaveBeen.UnitTests.Application;

public sealed class SharingHandlerTests
{
    [Fact]
    public async Task Only_a_hash_is_persisted_and_plaintext_is_returned_once()
    {
        var db = new TestStore(); var tokens = new TestTokens(); var handler = new CreateShareLinkHandler(db, tokens, db, new TestUser(), new TestClock());
        var result = await handler.HandleAsync(new("Friends", null), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess); Assert.Equal(43, result.Value!.Token.Length);
        var link = Assert.Single(db.Shares); Assert.NotEqual(result.Value.Token, link.TokenHash); Assert.Equal(tokens.Hash(result.Value.Token), link.TokenHash);
        Assert.Equal("alice", link.OwnerId); Assert.Null(link.ExpiresAt);
    }
    [Fact]
    public async Task A_previously_valid_session_stops_at_expiration_without_sleeping()
    {
        var db = new TestStore(); var tokens = new TestTokens(); var clock = new TestClock();
        var created = await new CreateShareLinkHandler(db, tokens, db, new TestUser(), clock).HandleAsync(new("Friends", clock.Now.AddHours(1)), TestContext.Current.CancellationToken);
        var credential = new ShareCredential(created.Value!.Id, tokens.Hash(created.Value.Token));
        var handler = new AuthorizeShareHandler(db, tokens, clock);
        Assert.NotNull(await handler.HandleAsync(credential, TestContext.Current.CancellationToken));
        clock.Now = clock.Now.AddHours(1);
        Assert.Null(await handler.HandleAsync(credential, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Foreign_revocation_is_denied_and_owner_revocation_invalidates_existing_sessions()
    {
        var db = new TestStore(); var tokens = new TestTokens(); var clock = new TestClock();
        var created = await new CreateShareLinkHandler(db, tokens, db, new TestUser(), clock).HandleAsync(new("Friends", null), TestContext.Current.CancellationToken);
        var credential = new ShareCredential(created.Value!.Id, tokens.Hash(created.Value.Token));
        var denied = await new RevokeShareLinkHandler(db, db, new TestUser("bob"), clock).HandleAsync(credential.Id, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorKind.NotFound, denied.Error?.Kind); Assert.Null(Assert.Single(db.Shares).RevokedAt);
        await new RevokeShareLinkHandler(db, db, new TestUser(), clock).HandleAsync(credential.Id, TestContext.Current.CancellationToken);
        Assert.Null(await new AuthorizeShareHandler(db, tokens, clock).HandleAsync(credential, TestContext.Current.CancellationToken));
    }
}
