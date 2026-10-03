using System.Security.Cryptography;
using IHaveBeen.Application.Common;
using IHaveBeen.Application.Media;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.UnitTests.Application;

public sealed class MediaHandlerTests
{
    [Fact]
    public async Task Short_stream_reads_preserve_every_original_byte_and_checksum()
    {
        var db = new TestStore(); var clock = new TestClock(); var log = TravelLog.Create("alice", Fixtures.Details(), clock.Now); db.Add(log);
        var storage = new TestStorage(); var handler = Handler(db, storage, clock);
        await using var stream = new ShortReadStream(Fixtures.Png);
        var result = await handler.HandleAsync(new(log.Id, stream, "../photo.png", "image/png", Fixtures.Png.Length, ""), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(Fixtures.Png, Assert.Single(storage.Objects).Value);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Fixtures.Png)), result.Value!.Sha256);
        Assert.Equal("photo.png", result.Value.OriginalName); Assert.Empty(db.Deletions); Assert.Single(db.Media); Assert.Equal(2, db.Commits);
    }
    [Fact]
    public async Task Storage_failure_keeps_durable_cleanup_without_publishing_media()
    {
        var db = new TestStore(); var clock = new TestClock(); var log = TravelLog.Create("alice", Fixtures.Details(), clock.Now); db.Add(log);
        var handler = Handler(db, new TestStorage { FailWrites = true }, clock);
        await using var stream = new MemoryStream(Fixtures.Png);
        await Assert.ThrowsAsync<IOException>(() => handler.HandleAsync(new(log.Id, stream, "file.png", "image/png", Fixtures.Png.Length, ""), TestContext.Current.CancellationToken));
        Assert.Empty(db.Media); var pending = Assert.Single(db.Deletions);
        Assert.Equal(clock.Now.AddHours(1), pending.Value.Due); Assert.Equal(1, db.Commits);
    }
    [Fact]
    public async Task Cross_owner_uploads_do_not_touch_storage_or_cleanup()
    {
        var db = new TestStore(); var clock = new TestClock(); var log = TravelLog.Create("bob", Fixtures.Details(), clock.Now); db.Add(log);
        var storage = new TestStorage(); await using var stream = new MemoryStream(Fixtures.Png);
        var result = await Handler(db, storage, clock).HandleAsync(new(log.Id, stream, "file.png", "image/png", Fixtures.Png.Length, ""), TestContext.Current.CancellationToken);
        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind); Assert.Empty(storage.Objects); Assert.Empty(db.Deletions); Assert.Equal(0, db.Commits);
    }
    [Fact]
    public async Task A_spoofed_mime_type_is_rejected_before_any_write()
    {
        var db = new TestStore(); var clock = new TestClock(); var log = TravelLog.Create("alice", Fixtures.Details(), clock.Now); db.Add(log);
        var storage = new TestStorage(); await using var stream = new MemoryStream(Fixtures.Png);
        var result = await Handler(db, storage, clock).HandleAsync(new(log.Id, stream, "file.jpg", "image/jpeg", Fixtures.Png.Length, ""), TestContext.Current.CancellationToken);
        Assert.Equal(ErrorKind.Validation, result.Error?.Kind); Assert.Empty(storage.Objects); Assert.Empty(db.Deletions);
    }
    private static UploadMediaHandler Handler(TestStore db, TestStorage storage, TestClock clock) =>
        new(db, db, storage, db, new TestFiles(), db, new TestUser(), clock, new(104857600), new TestMediaPolicy(), new TestScanner());
    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => base.ReadAsync(buffer[..Math.Min(3, buffer.Length)], ct);
    }
}
