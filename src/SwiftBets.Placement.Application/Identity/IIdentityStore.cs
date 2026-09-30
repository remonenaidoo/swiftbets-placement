namespace SwiftBets.Placement.Application.Identity;

public interface IIdentityStore
{
    Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task UpsertUserAsync(UserRecord user, CancellationToken cancellationToken);

    Task StoreRefreshTokenAsync(byte[] tokenHash, Guid userId, Guid familyId, DateTimeOffset expiresAt);

    /// <summary>Consumes a refresh token once; a second use reports <see cref="RefreshOutcome.Reused"/> and revokes its whole family.</summary>
    Task<(RefreshOutcome Outcome, Guid UserId, Guid FamilyId)> ConsumeRefreshTokenAsync(byte[] tokenHash, DateTimeOffset now);
}
