using IHaveBeen.Application.Accounts;
using IHaveBeen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IHaveBeen.Infrastructure.Identity;

internal sealed class IdentityGateway(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AppDbContext db) : IIdentityGateway
{
    public async Task<bool> LoginAsync(LoginAccount command)
    {
        var user = await users.FindByEmailAsync(command.Email);
        return user is not null && (await signIn.PasswordSignInAsync(user, command.Password, command.TrustDevice, true)).Succeeded;
    }
    public async Task<bool> RegisterAsync(RegisterAccount command)
    {
        var user = new ApplicationUser { UserName = command.Email, Email = command.Email, DisplayName = command.Name };
        if (!(await users.CreateAsync(user, command.Password)).Succeeded) return false;
        await signIn.SignInAsync(user, command.TrustDevice);
        return true;
    }
    public async Task LogoutAsync(string ownerId)
    {
        var user = await users.FindByIdAsync(ownerId);
        if (user is not null) await users.UpdateSecurityStampAsync(user);
        await signIn.SignOutAsync();
    }
    public Task<UserProfile?> GetProfileAsync(string ownerId, CancellationToken ct) =>
        db.Users.Where(x => x.Id == ownerId).Select(x => new UserProfile(x.DisplayName, x.Email)).FirstOrDefaultAsync(ct);
}
