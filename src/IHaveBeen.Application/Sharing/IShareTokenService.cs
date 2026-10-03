namespace IHaveBeen.Application.Sharing;

public interface IShareTokenService
{
    string Generate();
    string Hash(string token);
    bool Matches(string expectedHash, string actualHash);
}
