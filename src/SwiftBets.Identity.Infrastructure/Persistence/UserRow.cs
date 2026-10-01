using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Infrastructure.Persistence;

internal sealed record UserRow(
    Guid UserId, string? Email, string? Username, string PasswordHash, DateTime? DateOfBirth, string Brand, string Country, string Currency,
    byte Status, DateTimeOffset? EmailVerifiedAt, int FailedSignIns, DateTimeOffset? LockedUntil, Guid? ParentUserId, string? Roles)
{
    public User ToDomain() => new(
        UserId, Email, Username, PasswordHash, DateOfBirth is { } dob ? DateOnly.FromDateTime(dob) : null, Brand, Country, Currency,
        (AccountStatus)Status, EmailVerifiedAt, FailedSignIns, LockedUntil, ParentUserId,
        (Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries));
}
