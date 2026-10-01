namespace SwiftBets.Identity.Infrastructure.Messaging;

/// <summary>Plain-text account emails. Templates and brand styling move to the notifications service.</summary>
internal static class AccountEmails
{
    public static (string Subject, string Body) Verification(string link) => (
        "Confirm your email address",
        $"Welcome to SwiftBets.\n\nConfirm your email address by opening this link within 24 hours:\n{link}\n\nIf you did not create an account, ignore this email.");

    public static (string Subject, string Body) PasswordReset(string link) => (
        "Reset your password",
        $"Someone asked to reset the password on your SwiftBets account.\n\nChoose a new password with this link within the hour:\n{link}\n\nIf it was not you, ignore this email; your password is unchanged.");

    public static (string Subject, string Body) AlreadyRegistered(string signInLink, string resetLink) => (
        "You already have an account",
        $"Someone tried to open a SwiftBets account with this email address, which already has one.\n\nSign in: {signInLink}\nForgot your password? {resetLink}\n\nIf it was not you, ignore this email.");
}
