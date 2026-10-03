using IHaveBeen.Application.Abstractions;

namespace IHaveBeen.Application.Media;

public sealed class MediaContentReader(IObjectStorage storage)
{
    public Task<Stream> OpenAsync(MediaDownload media, CancellationToken cancellationToken, long? start = null, long? end = null) =>
        storage.OpenReadAsync(media.ObjectKey, cancellationToken, start, end);
}
