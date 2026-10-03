using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.Accounts;

public sealed class LogoutAccountHandler(IIdentityGateway identity, ICurrentUser user)
{
    public Task HandleAsync() => identity.LogoutAsync(user.RequireUserId());
}
