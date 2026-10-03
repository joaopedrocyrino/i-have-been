using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.Accounts;

public sealed class GetMyProfileHandler(IIdentityGateway identity, ICurrentUser user)
{
    public Task<UserProfile?> HandleAsync(CancellationToken cancellationToken) => identity.GetProfileAsync(user.RequireUserId(), cancellationToken);
}

