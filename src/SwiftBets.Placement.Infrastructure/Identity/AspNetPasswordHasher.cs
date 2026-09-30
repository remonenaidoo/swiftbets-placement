using Microsoft.AspNetCore.Identity;

namespace SwiftBets.Placement.Infrastructure.Identity;

/// <summary>PBKDF2 (ASP.NET Core Identity v3 format) with automatic rehash support.</summary>
public sealed class AspNetPasswordHasher : Application.Identity.IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object User = new();

    public string Hash(string password) => Hasher.HashPassword(User, password);

    public bool Verify(string hash, string password) => Hasher.VerifyHashedPassword(User, hash, password) != PasswordVerificationResult.Failed;
}
