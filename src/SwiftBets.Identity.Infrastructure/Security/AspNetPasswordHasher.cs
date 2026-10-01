using Microsoft.AspNetCore.Identity;

namespace SwiftBets.Identity.Infrastructure.Security;

/// <summary>PBKDF2 (ASP.NET Core Identity v3 format), the same format the pre-product identity module stored, so migrated hashes verify.</summary>
public sealed class AspNetPasswordHasher : Application.Ports.IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object User = new();

    public string Hash(string password) => Hasher.HashPassword(User, password);

    public bool Verify(string hash, string password) => Hasher.VerifyHashedPassword(User, hash, password) != PasswordVerificationResult.Failed;
}
