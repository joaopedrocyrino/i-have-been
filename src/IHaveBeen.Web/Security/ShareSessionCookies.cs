using System.Security.Cryptography;
using IHaveBeen.Application.Sharing;
using Microsoft.AspNetCore.DataProtection;

namespace IHaveBeen.Web.Security;

internal sealed class ShareSessionCookies(IDataProtectionProvider protection, IWebHostEnvironment environment, TimeProvider clock)
{
    private static string Name(Guid id) => $"ihb.share.{id:N}";
    public ShareCredential Read(HttpContext context, Guid id)
    {
        if (!context.Request.Cookies.TryGetValue(Name(id), out var value)) return new(id, null);
        try { return new(id, protection.CreateProtector("share", id.ToString()).Unprotect(value)); }
        catch (CryptographicException) { return new(id, null); }
    }
    public void Write(HttpContext context, ShareGrant grant)
    {
        var sessionEnd = clock.GetUtcNow().AddDays(30);
        var expires = grant.ExpiresAt is { } limit && limit < sessionEnd ? limit : sessionEnd;
        context.Response.Cookies.Append(Name(grant.Id), protection.CreateProtector("share", grant.Id.ToString()).Protect(grant.TokenHash), new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() || context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = $"/api/shared/{grant.Id}",
            Expires = expires
        });
    }
}
