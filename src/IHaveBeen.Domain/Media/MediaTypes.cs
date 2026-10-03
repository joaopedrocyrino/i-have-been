using System.Collections.Frozen;

namespace IHaveBeen.Domain.Media;

public static class MediaTypes
{
    public static IReadOnlySet<string> Supported { get; } = new HashSet<string>(StringComparer.Ordinal) {
        "image/jpeg", "image/png", "image/webp", "video/mp4", "video/quicktime", "video/webm"
    }.ToFrozenSet();
}
