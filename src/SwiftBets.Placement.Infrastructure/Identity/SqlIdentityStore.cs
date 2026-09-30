using Dapper;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Placement.Application.Identity;

namespace SwiftBets.Placement.Infrastructure.Identity;

public sealed class SqlIdentityStore(ISqlConnectionFactory connections) : IIdentityStore
{
    private static readonly SqlResources Sql = SqlResources.For<SqlIdentityStore>();

    public async Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return Map(await connection.QuerySingleOrDefaultAsync<(Guid UserId, string Username, string PasswordHash, string Roles)?>(
            new CommandDefinition(Sql.Get("Identity.FindUserByName"), new { Username = username }, cancellationToken: cancellationToken)));
    }

    public async Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return Map(await connection.QuerySingleOrDefaultAsync<(Guid UserId, string Username, string PasswordHash, string Roles)?>(
            new CommandDefinition(Sql.Get("Identity.FindUserById"), new { UserId = userId }, cancellationToken: cancellationToken)));
    }

    public async Task UpsertUserAsync(UserRecord user, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Identity.UpsertUser"),
            new { user.UserId, user.Username, user.PasswordHash, Roles = string.Join(',', user.Roles) }, cancellationToken: cancellationToken));
    }

    public async Task StoreRefreshTokenAsync(byte[] tokenHash, Guid userId, Guid familyId, DateTimeOffset expiresAt)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Identity.StoreRefresh"), new { TokenHash = tokenHash, UserId = userId, FamilyId = familyId, ExpiresAt = expiresAt });
    }

    public async Task<(RefreshOutcome Outcome, Guid UserId, Guid FamilyId)> ConsumeRefreshTokenAsync(byte[] tokenHash, DateTimeOffset now)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        var consumed = await connection.QuerySingleOrDefaultAsync<(Guid UserId, Guid FamilyId)?>(Sql.Get("Identity.ConsumeRefresh"), new { TokenHash = tokenHash, Now = now });
        if (consumed is { } token)
        {
            return (RefreshOutcome.Valid, token.UserId, token.FamilyId);
        }

        var reused = await connection.ExecuteScalarAsync<int>(Sql.Get("Identity.RevokeReusedFamily"), new { TokenHash = tokenHash, Now = now });
        return (reused == 1 ? RefreshOutcome.Reused : RefreshOutcome.Invalid, Guid.Empty, Guid.Empty);
    }

    private static UserRecord? Map((Guid UserId, string Username, string PasswordHash, string Roles)? row) =>
        row is { } r ? new UserRecord(r.UserId, r.Username, r.PasswordHash, r.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)) : null;
}
