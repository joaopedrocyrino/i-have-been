using Microsoft.AspNetCore.Identity;
using IHaveBeen.Domain.Accounts;

namespace IHaveBeen.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public UserType UserType { get; private set; }
}
