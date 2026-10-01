using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Application.Ports;

public interface IUserStore
{
    /// <summary>By normalized email, or by username for accounts migrated before emails existed.</summary>
    Task<User?> FindByLoginAsync(string login, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>False when the email or username is already taken; the unique index decides, not a prior read.</summary>
    Task<bool> CreateAsync(User user, DateTimeOffset now);

    /// <summary>Counts a failed sign-in and locks the account once it reaches the limit, atomically.</summary>
    Task RecordFailedSignInAsync(Guid userId, DateTimeOffset now);

    Task ClearFailedSignInsAsync(Guid userId);

    Task SetPasswordAsync(Guid userId, string passwordHash, DateTimeOffset now);

    Task MarkEmailVerifiedAsync(Guid userId, DateTimeOffset now);

    /// <summary>Moves the account to a new status only if it is still in <paramref name="from"/>, recording who and why.</summary>
    Task<bool> ChangeStatusAsync(Guid userId, AccountStatus from, AccountStatus to, string reason, string changedBy, DateTimeOffset now);

    Task<IReadOnlyList<string>> PermissionsForAsync(IReadOnlyList<string> roles, CancellationToken cancellationToken);

    Task UpsertSeedUserAsync(User user, DateTimeOffset now);
}
