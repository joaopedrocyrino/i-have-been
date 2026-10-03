using IHaveBeen.Application.Common;
using IHaveBeen.Application.Media;

namespace IHaveBeen.Application.Sharing;

public sealed class GetSharedMediaHandler(AuthorizeShareHandler authorization, IMediaRepository repository)
{
    public async Task<Result<MediaDownload>> HandleAsync(ShareCredential credential, Guid mediaId, CancellationToken cancellationToken)
    {
        var link = await authorization.HandleAsync(credential, cancellationToken);
        if (link is null) return Result<MediaDownload>.NotFound();
        var media = await repository.GetSharedAsync(mediaId, link.OwnerId, cancellationToken);
        return media is null ? Result<MediaDownload>.NotFound() : Result<MediaDownload>.Success(MediaDownload.From(media));
    }
}
