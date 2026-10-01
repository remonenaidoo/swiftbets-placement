using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Identity.Application.Ports;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Infrastructure.Persistence;

public sealed class SqlUserStore(ISqlConnectionFactory connections) : IUserStore
{
    private static readonly SqlResources Sql = SqlResources.For<SqlUserStore>();

    public async Task<User?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(Sql.Get("Users.FindByLogin"), new { Login = login }, cancellationToken: cancellationToken)))?.ToDomain();
    }

    public async Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(Sql.Get("Users.FindById"), new { UserId = userId }, cancellationToken: cancellationToken)))?.ToDomain();
    }

    public async Task<bool> CreateAsync(User user, DateTimeOffset now)
    {
        await using var connection = (SqlConnection)await connections.OpenAsync(CancellationToken.None);
        await using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(Sql.Get("Users.Insert"), Parameters(user, now), transaction);
            await connection.ExecuteAsync(Sql.Get("Users.InsertRole"), user.Roles.Select(r => new { user.UserId, Role = r }), transaction);
            await transaction.CommitAsync();
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task RecordFailedSignInAsync(Guid userId, DateTimeOffset now)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Users.RecordFailedSignIn"),
            new { UserId = userId, MaxFailures = User.MaxFailedSignIns, LockedUntil = now + User.LockoutDuration, Now = now });
    }

    public async Task ClearFailedSignInsAsync(Guid userId)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Users.ClearFailedSignIns"), new { UserId = userId });
    }

    public async Task SetPasswordAsync(Guid userId, string passwordHash, DateTimeOffset now)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Users.SetPassword"), new { UserId = userId, PasswordHash = passwordHash, Now = now });
    }

    public async Task MarkEmailVerifiedAsync(Guid userId, DateTimeOffset now)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Users.MarkEmailVerified"), new { UserId = userId, Now = now });
    }

    public async Task<bool> ChangeStatusAsync(Guid userId, AccountStatus from, AccountStatus to, string reason, string changedBy, DateTimeOffset now)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        return await connection.ExecuteScalarAsync<int>(Sql.Get("Users.ChangeStatus"),
            new { UserId = userId, From = (byte)from, To = (byte)to, Reason = reason, ChangedBy = changedBy, Now = now }) == 1;
    }

    public async Task<IReadOnlyList<string>> PermissionsForAsync(IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return [.. await connection.QueryAsync<string>(new CommandDefinition(Sql.Get("Users.PermissionsForRoles"), new { Roles = roles }, cancellationToken: cancellationToken))];
    }

    public async Task UpsertSeedUserAsync(User user, DateTimeOffset now)
    {
        await using var connection = (SqlConnection)await connections.OpenAsync(CancellationToken.None);
        await using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(Sql.Get("Users.UpsertSeed"), Parameters(user, now), transaction);
        await connection.ExecuteAsync(Sql.Get("Users.InsertRole"), user.Roles.Select(r => new { user.UserId, Role = r }), transaction);
        await transaction.CommitAsync();
    }

    private static object Parameters(User user, DateTimeOffset now) => new
    {
        user.UserId,
        user.Email,
        NormalizedEmail = user.Email is { } email ? EmailAddress.Normalize(email) : null,
        user.Username,
        user.PasswordHash,
        DateOfBirth = user.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
        user.Brand,
        user.Country,
        user.Currency,
        Status = (byte)user.Status,
        user.ParentUserId,
        Now = now,
    };
}
