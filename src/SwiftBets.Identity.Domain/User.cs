namespace SwiftBets.Identity.Domain;

/// <summary>
/// An account. Customers sign in by email; accounts migrated from the pre-product identity module may sign in by
/// username until they add an email. ParentUserId links a delegated (cashier) account to the account it acts for.
/// </summary>
public sealed record User(
    Guid UserId,
    string? Email,
    string? Username,
    string PasswordHash,
    DateOnly? DateOfBirth,
    string Brand,
    string Country,
    string Currency,
    AccountStatus Status,
    DateTimeOffset? EmailVerifiedAt,
    int FailedSignIns,
    DateTimeOffset? LockedUntil,
    Guid? ParentUserId,
    IReadOnlyList<string> Roles)
{
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public bool IsLockedAt(DateTimeOffset now) => LockedUntil is { } until && until > now;

    /// <summary>Why this account may not sign in, or null when it may.</summary>
    public string? SignInRefusal(DateTimeOffset now) =>
        IsLockedAt(now) ? "account_locked"
        : Status switch
        {
            AccountStatus.Active => null,
            AccountStatus.Suspended => "account_suspended",
            AccountStatus.Closed => "account_closed",
            AccountStatus.SelfExcluded => "account_self_excluded",
            _ => "account_unavailable",
        };
}
