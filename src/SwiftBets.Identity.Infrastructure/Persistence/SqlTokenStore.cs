using Dapper;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Identity.Application.Ports;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Infrastructure.Persistence;

public sealed class SqlTokenStore(ISqlConnectionFactory connections) : ITokenStore
{
    private static readonly SqlResources Sql = SqlResources.For<SqlTokenStore>();

    public async Task StoreRefreshTokenAsync(byte[] tokenHash, Guid userId, Guid familyId, DateTimeOffset expiresAt)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Tokens.StoreRefresh"), new { TokenHash = tokenHash, UserId = userId, FamilyId = familyId, ExpiresAt = expiresAt });
    }

    public async Task<(RefreshOutcome Outcome, Guid UserId, Guid FamilyId)> ConsumeRefreshTokenAsync(byte[] tokenHash, DateTimeOffset now)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        var consumed = await connection.QuerySingleOrDefaultAsync<(Guid UserId, Guid FamilyId)?>(Sql.Get("Tokens.ConsumeRefresh"), new { TokenHash = tokenHash, Now = now });
        if (consumed is { } token)
        {
            return (RefreshOutcome.Valid, token.UserId, token.FamilyId);
        }

        var reused = await connection.ExecuteScalarAsync<int>(Sql.Get("Tokens.RevokeReusedFamily"), new { TokenHash = tokenHash, Now = now });
        return (reused == 1 ? RefreshOutcome.Reused : RefreshOutcome.Invalid, Guid.Empty, Guid.Empty);
    }

    public async Task RevokeFamilyAsync(byte[] tokenHash, DateTimeOffset now)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Tokens.RevokeFamily"), new { TokenHash = tokenHash, Now = now });
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Tokens.RevokeAllForUser"), new { UserId = userId, Now = now });
    }

    public async Task StoreOneTimeTokenAsync(byte[] tokenHash, Guid userId, TokenPurpose purpose, DateTimeOffset expiresAt)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Tokens.StoreOneTime"), new { TokenHash = tokenHash, UserId = userId, Purpose = (byte)purpose, ExpiresAt = expiresAt });
    }

    public async Task<Guid?> ConsumeOneTimeTokenAsync(byte[] tokenHash, TokenPurpose purpose, DateTimeOffset now)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(Sql.Get("Tokens.ConsumeOneTime"), new { TokenHash = tokenHash, Purpose = (byte)purpose, Now = now });
    }
}
