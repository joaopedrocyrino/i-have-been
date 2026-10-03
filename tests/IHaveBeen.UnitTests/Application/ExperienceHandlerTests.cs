using IHaveBeen.Application.Common;
using IHaveBeen.Application.Experiences;
using IHaveBeen.Domain.Experiences;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.UnitTests.Application;

public sealed class ExperienceHandlerTests
{
    [Fact]
    public async Task Experience_create_edit_delete_is_owner_scoped_and_parent_scoped()
    {
        var store = new TestStore(); var user = new TestUser(); var clock = new TestClock();
        var log = TravelLog.Create("alice", Fixtures.Details(), clock.Now); store.Add(log);
        var details = new ExperienceDetails("Dinner", "Fresh fish", ExperienceCategory.Restaurant, 4, null, "");
        var create = new CreateExperienceHandler(store, store, store, user, clock);
        user.UserId = "bob"; Assert.Equal(ErrorKind.NotFound, (await create.HandleAsync(new(log.Id, details), TestContext.Current.CancellationToken)).Error!.Kind); Assert.Empty(store.Experiences);
        user.UserId = "alice"; var result = await create.HandleAsync(new(log.Id, details), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess); Assert.Single(log.Experiences); Assert.Equal(1, store.Commits);
        var id = result.Value!.Id; var update = new UpdateExperienceHandler(store, store, user, clock); var delete = new DeleteExperienceHandler(store, store, user);
        user.UserId = "bob";
        Assert.Equal(ErrorKind.NotFound, (await update.HandleAsync(new(log.Id, id, details), TestContext.Current.CancellationToken)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await delete.HandleAsync(log.Id, id, TestContext.Current.CancellationToken)).Error!.Kind);
        user.UserId = "alice";
        Assert.Equal(ErrorKind.NotFound, (await update.HandleAsync(new(Guid.NewGuid(), id, details), TestContext.Current.CancellationToken)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await delete.HandleAsync(Guid.NewGuid(), id, TestContext.Current.CancellationToken)).Error!.Kind);
        Assert.Equal(ErrorKind.Validation, (await update.HandleAsync(new(log.Id, id, details with { Rating = 6 }), TestContext.Current.CancellationToken)).Error!.Kind);
        Assert.Equal(4, store.Experiences.Single().Rating); Assert.Equal(1, store.Commits);
        Assert.True((await update.HandleAsync(new(log.Id, id, details with { Rating = 0 }), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(0, store.Experiences.Single().Rating);
        Assert.True((await delete.HandleAsync(log.Id, id, TestContext.Current.CancellationToken)).IsSuccess); Assert.Empty(store.Experiences); Assert.Equal(3, store.Commits);
    }
    [Fact]
    public async Task A_country_only_wish_cannot_receive_city_experiences()
    {
        var store = new TestStore(); var clock = new TestClock(); var log = TravelLog.Create("alice", Fixtures.Details() with { City = "", Status = TravelLogStatus.Wishlist, VisitedOn = null }, clock.Now); store.Add(log);
        var handler = new CreateExperienceHandler(store, store, store, new TestUser(), clock);
        var result = await handler.HandleAsync(new(log.Id, new("Bar", "", ExperienceCategory.Bar, 5, null, "")), TestContext.Current.CancellationToken);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind); Assert.Empty(store.Experiences); Assert.Equal(0, store.Commits);
    }
}
