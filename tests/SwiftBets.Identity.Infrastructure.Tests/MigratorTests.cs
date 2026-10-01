using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Testing;

namespace SwiftBets.Identity.Infrastructure.Tests;

public sealed class MigratorTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Migrator_creates_the_schema_and_is_idempotent()
    {
        var connectionString = await sql.CreateDatabaseAsync("idm_" + Guid.NewGuid().ToString("N")[..10]);

        (await IdentityDatabase.MigrateAsync(connectionString)).ShouldBe(0);
        (await IdentityDatabase.MigrateAsync(connectionString)).ShouldBe(0);

        (await TablesAsync(connectionString)).ShouldBe(["OneTimeTokens", "RefreshTokens", "RolePermissions", "StatusHistory", "UserRoles", "Users"]);
    }

    [Fact]
    public async Task Missing_connection_string_fails_with_a_usage_code()
    {
        var result = typeof(Program).Assembly.EntryPoint!.Invoke(null, [Array.Empty<string>()]);
        (result is Task<int> task ? await task : (int)result!).ShouldBe(2);
    }

    [Fact]
    public async Task Every_migration_rolls_back_newest_first_and_reapplies()
    {
        var connectionString = await sql.CreateDatabaseAsync("idm_" + Guid.NewGuid().ToString("N")[..10]);
        (await IdentityDatabase.MigrateAsync(connectionString)).ShouldBe(0);
        await using var connection = new SqlConnection(connectionString);

        await connection.ExecuteAsync(Rollback("0003_role_permissions"));
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM accounts.RolePermissions")).ShouldBe(0);
        await connection.ExecuteAsync(Rollback("0002_users_and_tokens"));
        (await TablesAsync(connectionString)).ShouldBeEmpty();

        (await IdentityDatabase.MigrateAsync(connectionString)).ShouldBe(0);
        (await TablesAsync(connectionString)).Count.ShouldBe(6);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM accounts.RolePermissions")).ShouldBe(5);
    }

    [Fact]
    public async Task Placement_accounts_and_their_live_devices_move_across_and_keep_working()
    {
        var placement = await sql.CreateDatabaseAsync("plc_" + Guid.NewGuid().ToString("N")[..10]);
        var identity = await sql.CreateDatabaseAsync("idm_" + Guid.NewGuid().ToString("N")[..10]);
        var db = await PlacementWithOneUserAsync(placement);

        (await IdentityDatabase.MigrateAsync(identity, $"--Migrator:ImportFromPlacement={placement}")).ShouldBe(0);
        (await IdentityDatabase.MigrateAsync(identity, $"--Migrator:ImportFromPlacement={placement}")).ShouldBe(0);

        var target = IdentityDatabase.For(identity);
        (await target.Sessions.PasswordAsync("operator9", "the old password", CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await target.Sessions.RefreshAsync(db.RefreshToken, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        var user = (await target.Users.FindByLoginAsync("operator9", CancellationToken.None))!;
        user.Roles.ShouldBe(["Operator"]);
    }

    private static async Task<(Guid UserId, string RefreshToken)> PlacementWithOneUserAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync("""
            CREATE SCHEMA auth;
            """);
        await connection.ExecuteAsync("""
            CREATE TABLE auth.Users (UserId uniqueidentifier PRIMARY KEY, Username nvarchar(100) NOT NULL, PasswordHash nvarchar(500) NOT NULL, Roles nvarchar(200) NOT NULL, CreatedAt datetimeoffset(3) NOT NULL);
            CREATE TABLE auth.RefreshTokens (TokenHash binary(32) PRIMARY KEY, UserId uniqueidentifier NOT NULL, FamilyId uniqueidentifier NOT NULL, ExpiresAt datetimeoffset(3) NOT NULL, ConsumedAt datetimeoffset(3) NULL, RevokedAt datetimeoffset(3) NULL);
            """);
        var userId = Guid.NewGuid();
        var refresh = "an-old-refresh-token";
        await connection.ExecuteAsync("INSERT INTO auth.Users VALUES (@UserId, 'operator9', @Hash, 'Operator', SYSUTCDATETIME())",
            new { UserId = userId, Hash = new Security.AspNetPasswordHasher().Hash("the old password") });
        await connection.ExecuteAsync("INSERT INTO auth.RefreshTokens VALUES (@Hash, @UserId, NEWID(), DATEADD(day, 7, SYSUTCDATETIME()), NULL, NULL)",
            new { Hash = Application.SecretTokens.Hash(refresh), UserId = userId });
        return (userId, refresh);
    }

    private static async Task<List<string>> TablesAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        return [.. (await connection.QueryAsync<string>("SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('accounts')")).Order(StringComparer.Ordinal)];
    }

    private static string Rollback(string migration)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream($"SwiftBets.Identity.Migrator.Rollbacks.{migration}.sql")!;
        return new StreamReader(stream).ReadToEnd();
    }
}
