namespace SwiftBets.Identity.Domain;

/// <summary>Length over composition rules: at least 10 characters, at most 128, and not the email address itself.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 128;

    public static string? Problem(string password, string email) =>
        password.Length < MinimumLength ? $"Use at least {MinimumLength} characters."
        : password.Length > MaximumLength ? $"Use at most {MaximumLength} characters."
        : string.Equals(password.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase) ? "Do not use your email address as your password."
        : null;
}
