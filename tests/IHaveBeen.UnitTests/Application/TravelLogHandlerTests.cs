using IHaveBeen.Application.Common;
using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Domain.Media;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.UnitTests.Application;

public sealed class TravelLogHandlerTests
{
    [Fact]
    public async Task A_foreign_owner_cannot_edit_or_commit_a_memory()
    {
        var db = new TestStore(); var clock = new TestClock(); var log = TravelLog.Create("alice", Fixtures.Details(), clock.Now); db.Add(log);
        var handler = new UpdateTravelLogHandler(db, db, new TestUser("bob"), clock);
        var result = await handler.HandleAsync(new(log.Id, Fixtures.Details() with { Title = "Stolen" }), TestContext.Current.CancellationToken);
        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind); Assert.Equal("Rio", log.Title); Assert.Equal(0, db.Commits);
    }
    [Fact]
    public async Task Creation_uses_the_current_identity_and_injected_clock()
    {
        var db = new TestStore(); var clock = new TestClock(); var handler = new CreateTravelLogHandler(db, db, new TestUser("alice"), clock);
        var result = await handler.HandleAsync(new(Fixtures.Details()), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess); var log = Assert.Single(db.Logs);
        Assert.Equal("alice", log.OwnerId); Assert.Equal(clock.Now, log.CreatedAt); Assert.Equal(1, db.Commits);
    }
    [Fact]
    public async Task Invalid_creation_never_mutates_persistence()
    {
        var db = new TestStore(); var handler = new CreateTravelLogHandler(db, db, new TestUser(), new TestClock());
        var result = await handler.HandleAsync(new(Fixtures.Details() with { Title = "" }), TestContext.Current.CancellationToken);
        Assert.Equal(ErrorKind.Validation, result.Error?.Kind); Assert.Empty(db.Logs); Assert.Equal(0, db.Commits);
    }
    [Fact]
    public async Task Missing_authentication_cannot_reach_persistence()
    {
        var db = new TestStore(); var handler = new CreateTravelLogHandler(db, db, new TestUser(null), new TestClock());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.HandleAsync(new(Fixtures.Details()), TestContext.Current.CancellationToken));
        Assert.Empty(db.Logs); Assert.Equal(0, db.Commits);
    }
    [Fact]
    public async Task Deleting_a_memory_enqueues_its_objects_in_the_same_commit()
    {
        var db = new TestStore(); var clock = new TestClock(); var log = TravelLog.Create("alice", Fixtures.Details(), clock.Now);
        log.AttachMedia(MediaAsset.Create(Guid.NewGuid(), log.Id, "private/key", "file.png", "image/png", 10, new string('a', 64), "", 0, clock.Now)); db.Add(log);
        var handler = new DeleteTravelLogHandler(db, db, db, new TestUser(), clock);
        Assert.True((await handler.HandleAsync(new(log.Id), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Empty(db.Logs); Assert.Equal("private/key", Assert.Single(db.Deletions).Value.Key); Assert.Equal(1, db.Commits);
    }
}
