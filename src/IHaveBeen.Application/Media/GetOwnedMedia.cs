using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.Media;

public sealed class GetOwnedMediaHandler(IMediaRepository repository, ICurrentUser user)
{
    public async Task<Result<MediaDownload>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await repository.GetOwnedAsync(id, user.RequireUserId(), cancellationToken);
        return media is null ? Result<MediaDownload>.NotFound() : Result<MediaDownload>.Success(MediaDownload.From(media));
    }
}
