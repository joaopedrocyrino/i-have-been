using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using IHaveBeen.Application.Observability;
using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;
using IHaveBeen.Application.Media;
using IHaveBeen.Domain.Accounts;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.UnitTests.Application;

public sealed class UploadProtectionTests
{
    [Theory]
    [InlineData(MalwareScanResult.Rejected, ErrorKind.Validation, "malware")]
    [InlineData(MalwareScanResult.Unavailable, ErrorKind.Unavailable, "scannerunavailable")]
    public async Task Unverified_files_never_reach_storage_or_create_database_records(MalwareScanResult verdict, ErrorKind error, string expectedOutcome)
    {
        var outcomes = new ConcurrentBag<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, subscribed) =>
        { if (instrument.Meter.Name == ApplicationTelemetry.Name && instrument.Name == "ihb.uploads") subscribed.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            { Assert.Equal("outcome", tag.Key); outcomes.Add(tag.Value!.ToString()!); }
        });
        listener.Start();
        var (db, log) = Store(); var storage = new TestStorage(); var scanner = new TestScanner(verdict);
        await using var file = new MemoryStream(Fixtures.Png);
        var result = await Handler(db, storage, scanner).HandleAsync(new(log.Id, file, "file.png", "image/png", file.Length, ""), TestContext.Current.CancellationToken);
        Assert.Equal(error, result.Error?.Kind); Assert.Empty(storage.Objects); Assert.Empty(db.Media); Assert.Empty(db.Deletions);
        Assert.Equal(0, db.Commits); Assert.Equal(Fixtures.Png, scanner.Scanned);
        Assert.Contains(expectedOutcome, outcomes);
    }
    [Fact]
    public async Task Clean_scanned_bytes_are_preserved_exactly()
    {
        var (db, log) = Store(); var storage = new TestStorage(); var scanner = new TestScanner();
        await using var file = new MemoryStream(Fixtures.Png);
        var result = await Handler(db, storage, scanner).HandleAsync(new(log.Id, file, "file.png", "image/png", file.Length, ""), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess); Assert.Equal(Fixtures.Png, scanner.Scanned); Assert.Equal(scanner.Scanned, Assert.Single(storage.Objects).Value);
    }
    [Theory]
    [InlineData(UserType.User, 100, false)]
    [InlineData(UserType.User, 99, true)]
    [InlineData(UserType.Manager, 1000, true)]
    public async Task Quota_applies_to_regular_accounts_and_managers_bypass_the_count(UserType type, int count, bool accepted)
    {
        var (db, log) = Store(); var storage = new TestStorage(); var scanner = new TestScanner();
        await using var file = new MemoryStream(Fixtures.Png);
        var result = await Handler(db, storage, scanner, new(type, count)).HandleAsync(new(log.Id, file, "file.png", "image/png", file.Length, ""), TestContext.Current.CancellationToken);
        Assert.Equal(accepted, result.IsSuccess); Assert.Equal(accepted ? 1 : 0, scanner.Calls);
        if (!accepted) { Assert.Equal(ErrorKind.Conflict, result.Error?.Kind); Assert.Empty(storage.Objects); }
    }
    [Theory]
    [InlineData(UserType.User, false)]
    [InlineData(UserType.Manager, true)]
    public async Task Manager_files_use_the_safety_cap_instead_of_the_user_photo_cap(UserType type, bool accepted)
    {
        var (db, log) = Store(); var scanner = new TestScanner(); var storage = new TestStorage();
        await using var file = new MemoryStream(Fixtures.Png);
        var result = await Handler(db, storage, scanner, new(type), new(1024, 32, 512)).HandleAsync(new(log.Id, file, "file.png", "image/png", file.Length, ""), TestContext.Current.CancellationToken);
        Assert.Equal(accepted, result.IsSuccess); if (!accepted) Assert.Equal(ErrorKind.TooLarge, result.Error?.Kind);
    }
    [Fact]
    public async Task Understated_metadata_cannot_make_an_oversized_stream_reach_the_scanner()
    {
        var (db, log) = Store(); var scanner = new TestScanner(); var storage = new TestStorage();
        await using var file = new MemoryStream(Fixtures.Png.Concat(new byte[200]).ToArray());
        var result = await Handler(db, storage, scanner, limits: new(1024, 100, 512)).HandleAsync(new(log.Id, file, "file.png", "image/png", 80, ""), TestContext.Current.CancellationToken);
        Assert.Equal(ErrorKind.TooLarge, result.Error?.Kind); Assert.Equal(0, scanner.Calls); Assert.Empty(storage.Objects); Assert.Empty(db.Deletions);
    }
    [Fact]
    public async Task Incorrect_actual_lengths_are_rejected_without_scanning()
    {
        var (db, log) = Store(); var scanner = new TestScanner(); var storage = new TestStorage();
        await using var file = new MemoryStream(Fixtures.Png);
        var result = await Handler(db, storage, scanner).HandleAsync(new(log.Id, file, "file.png", "image/png", file.Length + 1, ""), TestContext.Current.CancellationToken);
        Assert.Equal(ErrorKind.Validation, result.Error?.Kind); Assert.Equal(0, scanner.Calls); Assert.Empty(storage.Objects);
    }
    [Theory]
    [InlineData("image/png", 20)]
    [InlineData("video/mp4", 100)]
    public void Default_size_limits_are_distinct_for_photos_and_videos(string type, int mib) =>
        Assert.Equal(mib * 1048576L, new MediaLimits(104857600).ForFile(UserType.User, type));
    private static (TestStore Db, TravelLog Log) Store()
    {
        var db = new TestStore(); var log = TravelLog.Create("alice", Fixtures.Details(), new TestClock().Now); db.Add(log); return (db, log);
    }
    private static UploadMediaHandler Handler(TestStore db, TestStorage storage, TestScanner scanner,
        TestMediaPolicy? policy = null, MediaLimits? limits = null) =>
        new(db, db, storage, db, new TestFiles(), db, new TestUser(), new TestClock(), limits ?? new(104857600), policy ?? new(), scanner);
}
