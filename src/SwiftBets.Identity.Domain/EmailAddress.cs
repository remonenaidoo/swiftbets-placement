namespace SwiftBets.Identity.Domain;

/// <summary>A customer's email, compared case-insensitively. Validation is deliberately loose: the verification email is the real test.</summary>
public static class EmailAddress
{
    public const int MaxLength = 254;

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public static bool IsPlausible(string email)
    {
        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@', StringComparison.Ordinal);
        return trimmed.Length <= MaxLength && at > 0 && at == trimmed.LastIndexOf('@') && at < trimmed.Length - 1
            && trimmed[(at + 1)..].Contains('.', StringComparison.Ordinal) && !trimmed.Any(char.IsWhiteSpace);
    }
}
