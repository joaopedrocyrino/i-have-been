using IHaveBeen.Domain.Accounts;

namespace IHaveBeen.Application.Media;

public sealed record MediaLimits(long MaxUploadBytes, long UserPhotoMaxBytes = 20 * 1024 * 1024,
    long UserVideoMaxBytes = 100 * 1024 * 1024)
{
    // This account-wide ceiling is also enforced atomically by PostgreSQL.
    public const int UserMediaCount = 100;
    public long ForFile(UserType type, string contentType) => type == UserType.Manager ? MaxUploadBytes
        : Math.Min(MaxUploadBytes, contentType.StartsWith("image/", StringComparison.Ordinal) ? UserPhotoMaxBytes : UserVideoMaxBytes);
}

public sealed record AccountMediaUsage(UserType UserType, int Count);
public sealed record MediaUsage(string UserType, int Count, int? Limit, long PhotoMaxBytes, long VideoMaxBytes);

public interface IAccountMediaPolicy
{
    Task<AccountMediaUsage?> GetAsync(string ownerId, CancellationToken cancellationToken);
}

public sealed class MediaQuotaExceededException : Exception;
