using IHaveBeen.Domain.Common;
using IHaveBeen.Domain.TravelLogs;

namespace IHaveBeen.Domain.Media;

public sealed class MediaAsset
{
    private MediaAsset() { }
    public Guid Id { get; private set; }
    public Guid TravelLogId { get; private set; }
    public TravelLog TravelLog { get; private set; } = null!;
    public string ObjectKey { get; private set; } = "";
    public string OriginalName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long ByteLength { get; private set; }
    public string Sha256 { get; private set; } = "";
    public string Caption { get; private set; } = "";
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static MediaAsset Create(Guid id, Guid logId, string key, string name, string type, long bytes,
        string checksum, string caption, int order, DateTimeOffset now)
    {
        if (id == Guid.Empty || logId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 300
            || string.IsNullOrWhiteSpace(name) || name.Length > 200 || bytes <= 0 || caption.Length > 500 || order < 0
            || checksum.Length != 64 || !checksum.All(Uri.IsHexDigit) || !MediaTypes.Supported.Contains(type))
            throw new DomainRuleException("Invalid original media metadata.");
        return new MediaAsset
        {
            Id = id,
            TravelLogId = logId,
            ObjectKey = key,
            OriginalName = name,
            ContentType = type,
            ByteLength = bytes,
            Sha256 = checksum,
            Caption = caption,
            SortOrder = order,
            CreatedAt = now
        };
    }
}
