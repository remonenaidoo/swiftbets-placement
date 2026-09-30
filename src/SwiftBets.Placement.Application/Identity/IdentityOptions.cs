using System.ComponentModel.DataAnnotations;

namespace SwiftBets.Placement.Application.Identity;

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = "swiftbets";

    /// <summary>PKCS#8 PEM; empty only in Development, where a key is generated per process.</summary>
    public string SigningKeyPem { get; set; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 10;

    [Range(1, 30)]
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>Service clients for client_credentials: client id to secret; each is granted the Service role.</summary>
    public Dictionary<string, string> Clients { get; set; } = [];

    public bool SeedDemoUsers { get; set; }

    public string DemoPassword { get; set; } = string.Empty;
}
