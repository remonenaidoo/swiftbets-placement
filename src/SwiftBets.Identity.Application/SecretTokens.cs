using System.Security.Cryptography;
using System.Text;

namespace SwiftBets.Identity.Application;

/// <summary>Random bearer secrets (refresh, verification, reset) and the hash they are stored under.</summary>
public static class SecretTokens
{
    public static string New() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
