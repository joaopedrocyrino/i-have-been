using IHaveBeen.Application.Abstractions;
using IHaveBeen.Application.Common;

namespace IHaveBeen.Application.Accounts;

public sealed class RegisterAccountHandler(IIdentityGateway identity, AccountPolicy policy)
{
    public async Task<Result<Unit>> HandleAsync(RegisterAccount command)
    {
        if (!policy.AllowRegistration) return Result<Unit>.Forbidden();
        if (command.Email is not { Length: <= 254 } || string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 80 || command.Password is not { Length: <= 128 })
            return Result<Unit>.Invalid("Unable to create the account.");
        var created = await identity.RegisterAsync(command with { Email = command.Email.Trim(), Name = command.Name.Trim() });
        return created ? Result<Unit>.Success(default) : Result<Unit>.Invalid("Unable to create the account.");
    }
}
