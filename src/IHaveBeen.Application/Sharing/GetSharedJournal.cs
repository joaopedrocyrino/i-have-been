using IHaveBeen.Application.Accounts;
using IHaveBeen.Application.Common;
using IHaveBeen.Application.TravelLogs;

namespace IHaveBeen.Application.Sharing;

public sealed record SharedJournal(string Name, DateTimeOffset? ExpiresAt, IReadOnlyList<TravelLogDto> Logs);
public sealed class GetSharedJournalHandler(AuthorizeShareHandler authorization, ITravelLogRepository logs, IIdentityGateway identity)
{
    public async Task<Result<SharedJournal>> HandleAsync(ShareCredential credential, CancellationToken cancellationToken)
    {
        var link = await authorization.HandleAsync(credential, cancellationToken);
        if (link is null) return Result<SharedJournal>.NotFound();
        var profile = await identity.GetProfileAsync(link.OwnerId, cancellationToken);
        if (profile is null) return Result<SharedJournal>.NotFound();
        var memories = await logs.ListSharedAsync(link.OwnerId, cancellationToken);
        return Result<SharedJournal>.Success(new(profile.DisplayName, link.ExpiresAt, memories.Select(TravelLogDto.From).ToList()));
    }
}
