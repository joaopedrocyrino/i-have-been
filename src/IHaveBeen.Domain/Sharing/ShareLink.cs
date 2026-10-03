using IHaveBeen.Domain.Common;

namespace IHaveBeen.Domain.Sharing;

public sealed class ShareLink
{
    private ShareLink() { }
    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = "";
    public string Label { get; private set; } = "";
    public string TokenHash { get; private set; } = "";
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ShareLink Create(string ownerId, string label, string hash, DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(label) || label.Length > 80
            || hash.Length != 64 || !hash.All(Uri.IsHexDigit) || expiresAt <= now)
            throw new DomainRuleException("Choose a label and a future expiration, or no expiration.");
        return new ShareLink
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Label = label.Trim(),
            TokenHash = hash,
            ExpiresAt = expiresAt?.ToUniversalTime(),
            CreatedAt = now
        };
    }
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
