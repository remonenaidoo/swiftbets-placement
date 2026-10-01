using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Application.Ports;

public enum RefreshOutcome
{
    Valid,
    Reused,
    Invalid,
}

/// <summary>Refresh tokens and emailed single-use tokens, stored only as SHA-256 hashes.</summary>
public interface ITokenStore
{
    Task StoreRefreshTokenAsync(byte[] tokenHash, Guid userId, Guid familyId, DateTimeOffset expiresAt);

    /// <summary>Consumes a refresh token once; a second use reports <see cref="RefreshOutcome.Reused"/> and revokes its whole family.</summary>
    Task<(RefreshOutcome Outcome, Guid UserId, Guid FamilyId)> ConsumeRefreshTokenAsync(byte[] tokenHash, DateTimeOffset now);

    Task RevokeFamilyAsync(byte[] tokenHash, DateTimeOffset now);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now);

    Task StoreOneTimeTokenAsync(byte[] tokenHash, Guid userId, TokenPurpose purpose, DateTimeOffset expiresAt);

    /// <summary>Marks the token used and returns its user, once; null when unknown, expired, used or for another purpose.</summary>
    Task<Guid?> ConsumeOneTimeTokenAsync(byte[] tokenHash, TokenPurpose purpose, DateTimeOffset now);
}
