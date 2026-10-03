using System.Security.Cryptography;
using System.Text;
using IHaveBeen.Application.Sharing;

namespace IHaveBeen.Infrastructure.Security;

internal sealed class ShareTokenService : IShareTokenService
{
    public string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public bool Matches(string a, string b) => a.Length == b.Length && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
