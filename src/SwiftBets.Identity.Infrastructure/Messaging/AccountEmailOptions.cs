namespace SwiftBets.Identity.Infrastructure.Messaging;

public sealed class AccountEmailOptions
{
    public const string SectionName = "AccountEmail";

    /// <summary>Empty sends nothing; links are logged in Development only.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 1025;

    public bool UseTls { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string From { get; set; } = "SwiftBets <no-reply@swiftbets.local>";

    /// <summary>The customer website's origin; account links point at its pages.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:7100";
}
