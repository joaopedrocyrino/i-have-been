namespace IHaveBeen.Application.Accounts;

public sealed record UserProfile(string DisplayName, string? Email);
public sealed record LoginAccount(string Email, string Password, bool TrustDevice = false);
public sealed record RegisterAccount(string Name, string Email, string Password, bool TrustDevice = false);
public sealed record AccountPolicy(bool AllowRegistration);
public interface IIdentityGateway
{
    Task<bool> LoginAsync(LoginAccount command);
    Task<bool> RegisterAsync(RegisterAccount command);
    Task LogoutAsync(string ownerId);
    Task<UserProfile?> GetProfileAsync(string ownerId, CancellationToken cancellationToken);
}
