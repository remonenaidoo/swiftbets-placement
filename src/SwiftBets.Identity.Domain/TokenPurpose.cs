namespace SwiftBets.Identity.Domain;

/// <summary>Single-use emailed tokens. Stored as a byte.</summary>
public enum TokenPurpose : byte
{
    VerifyEmail = 1,
    ResetPassword = 2,
}
