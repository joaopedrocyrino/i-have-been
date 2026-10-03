using IHaveBeen.Domain.Media;

namespace IHaveBeen.Application.Media;

public sealed record MediaDto(Guid Id, string OriginalName, string ContentType, long ByteLength, string Sha256, string Caption, int SortOrder)
{
    public static MediaDto From(MediaAsset media) => new(media.Id, media.OriginalName, media.ContentType, media.ByteLength, media.Sha256, media.Caption, media.SortOrder);
}
// Internal delivery metadata: never serialized as an HTTP response.
public sealed record MediaDownload(string ObjectKey, string OriginalName, string ContentType, long ByteLength)
{
    public static MediaDownload From(MediaAsset media) => new(media.ObjectKey, media.OriginalName, media.ContentType, media.ByteLength);
}
