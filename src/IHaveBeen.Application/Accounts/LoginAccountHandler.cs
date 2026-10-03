using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.Accounts;

public sealed class LoginAccountHandler(IIdentityGateway identity)
{
    public Task<bool> HandleAsync(LoginAccount command) =>
        command.Email is { Length: <= 254 } && command.Password is { Length: <= 128 }
            ? identity.LoginAsync(command with { Email = command.Email.Trim() }) : Task.FromResult(false);
}
