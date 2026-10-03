using System.Security.Cryptography;
using System.Text;
using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Experiences;
using IHaveBeen.Application.Media;
using IHaveBeen.Application.Sharing;
using IHaveBeen.Application.TravelLogs;
using IHaveBeen.Domain.Experiences;
using IHaveBeen.Domain.Media;
using IHaveBeen.Domain.Sharing;
using IHaveBeen.Domain.TravelLogs;
using IHaveBeen.Domain.Accounts;

namespace IHaveBeen.UnitTests;

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-02T12:00:00Z");
    public override DateTimeOffset GetUtcNow() => Now;
}
internal sealed class TestUser(string? id = "alice") : ICurrentUser { public string? UserId { get; set; } = id; }
internal sealed class TestTokens : IShareTokenService
{
    public string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public bool Matches(string a, string b) => a.Length == b.Length && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
internal sealed class TestFiles : ITemporaryFileFactory { public Stream Create() => new MemoryStream(); }
internal sealed class TestMediaPolicy(UserType type = UserType.User, int count = 0) : IAccountMediaPolicy
{
    public Task<AccountMediaUsage?> GetAsync(string ownerId, CancellationToken ct) => Task.FromResult<AccountMediaUsage?>(new(type, count));
}
internal sealed class TestScanner(MalwareScanResult result = MalwareScanResult.Clean) : IMalwareScanner
{
    public int Calls { get; private set; }
    public byte[]? Scanned { get; private set; }
    public async Task<MalwareScanResult> ScanAsync(Stream original, CancellationToken ct)
    {
        Calls++; using var copy = new MemoryStream(); await original.CopyToAsync(copy, ct); Scanned = copy.ToArray(); return result;
    }
}
internal sealed class TestStorage : IObjectStorage
{
    public Dictionary<string, byte[]> Objects { get; } = [];
    public bool FailWrites { get; set; }
    public async Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct)
    {
        if (FailWrites) throw new IOException("Storage offline");
        using var copy = new MemoryStream(); await stream.CopyToAsync(copy, ct); Objects[key] = copy.ToArray();
    }
    public Task<Stream> OpenReadAsync(string key, CancellationToken ct, long? start = null, long? end = null) =>
        Task.FromResult<Stream>(new MemoryStream(Objects[key]));
    public Task DeleteAsync(string key, CancellationToken ct) { Objects.Remove(key); return Task.CompletedTask; }
}
internal sealed class TestStore : ITravelLogRepository, IExperienceRepository, IMediaRepository, IShareLinkRepository, IObjectDeletionQueue, IUnitOfWork
{
    public List<TravelLog> Logs { get; } = [];
    public List<Experience> Experiences { get; } = [];
    public void Add(Experience experience) => Experiences.Add(experience);
    public void Remove(Experience experience) => Experiences.Remove(experience);
    Task<Experience?> IExperienceRepository.GetOwnedAsync(Guid id, string owner, CancellationToken ct) => Task.FromResult(Experiences.FirstOrDefault(x => x.Id == id && Logs.Any(l => l.Id == x.TravelLogId && l.OwnerId == owner)));
    public List<MediaAsset> Media { get; } = [];
    public List<ShareLink> Shares { get; } = [];
    public Dictionary<Guid, (string Key, DateTimeOffset Due)> Deletions { get; } = [];
    public int Commits { get; private set; }
    public Task SaveChangesAsync(CancellationToken ct) { Commits++; return Task.CompletedTask; }
    public void Add(TravelLog log) => Logs.Add(log);
    public void Remove(TravelLog log) => Logs.Remove(log);
    Task<TravelLog?> ITravelLogRepository.GetOwnedAsync(Guid id, string owner, CancellationToken ct) => Task.FromResult(Logs.FirstOrDefault(x => x.Id == id && x.OwnerId == owner));
    Task<IReadOnlyList<TravelLog>> ITravelLogRepository.ListOwnedAsync(string owner, CancellationToken ct) => Task.FromResult<IReadOnlyList<TravelLog>>(Logs.Where(x => x.OwnerId == owner).ToList());
    Task<IReadOnlyList<TravelLog>> ITravelLogRepository.ListSharedAsync(string owner, CancellationToken ct) => Task.FromResult<IReadOnlyList<TravelLog>>(Logs.Where(x => x.OwnerId == owner && x.IncludeInShares).ToList());
    public Task<bool> ExistsOwnedAsync(Guid id, string owner, CancellationToken ct) => Task.FromResult(Logs.Any(x => x.Id == id && x.OwnerId == owner));
    public void Add(MediaAsset media) => Media.Add(media);
    public void Remove(MediaAsset media) => Media.Remove(media);
    Task<MediaAsset?> IMediaRepository.GetOwnedAsync(Guid id, string owner, CancellationToken ct) => Task.FromResult(Media.FirstOrDefault(x => x.Id == id && Logs.Any(l => l.Id == x.TravelLogId && l.OwnerId == owner)));
    public Task<MediaAsset?> GetSharedAsync(Guid id, string owner, CancellationToken ct) => Task.FromResult(Media.FirstOrDefault(x => x.Id == id && Logs.Any(l => l.Id == x.TravelLogId && l.OwnerId == owner && l.IncludeInShares)));
    public Task<int> CountForLogAsync(Guid id, CancellationToken ct) => Task.FromResult(Media.Count(x => x.TravelLogId == id));
    public void Add(ShareLink link) => Shares.Add(link);
    Task<ShareLink?> IShareLinkRepository.GetOwnedAsync(Guid id, string owner, CancellationToken ct) => Task.FromResult(Shares.FirstOrDefault(x => x.Id == id && x.OwnerId == owner));
    Task<IReadOnlyList<ShareLink>> IShareLinkRepository.ListOwnedAsync(string owner, CancellationToken ct) => Task.FromResult<IReadOnlyList<ShareLink>>(Shares.Where(x => x.OwnerId == owner).ToList());
    public Task<ShareLink?> GetActiveAsync(Guid id, DateTimeOffset now, CancellationToken ct) => Task.FromResult(Shares.FirstOrDefault(x => x.Id == id && x.IsActive(now)));
    public Guid Enqueue(string key, DateTimeOffset now, DateTimeOffset? notBefore = null) { var id = Guid.NewGuid(); Deletions.Add(id, (key, notBefore ?? now)); return id; }
    public Task RemoveAsync(Guid id, CancellationToken ct) { Deletions.Remove(id); return Task.CompletedTask; }
    public Task<IReadOnlyList<ObjectDeletion>> GetDueAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ObjectDeletion>>(Deletions.Where(x => x.Value.Due <= now).Take(limit).Select(x => new ObjectDeletion(x.Key, x.Value.Key)).ToList());
}
internal static class Fixtures
{
    public static TravelLogDetails Details(bool shared = false) => new("Rio", "A memory", "Rio de Janeiro", "Brazil", -22.9068, -43.1729, new DateOnly(2026, 5, 24), null, shared);
    public static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
}
