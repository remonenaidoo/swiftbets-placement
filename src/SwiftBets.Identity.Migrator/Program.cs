using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SwiftBets.BuildingBlocks.Persistence;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
var connectionString = configuration["ConnectionStrings:SbIdentity"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    await Console.Error.WriteLineAsync("ConnectionStrings:SbIdentity is required.");
    return 2;
}

var source = new MigrationSource(typeof(Program).Assembly, 1);
var result = MigrationRunner.RunSqlServer(connectionString, configuration.GetValue("Migrator:EnsureDatabase", false), source);
if (!result.Successful)
{
    await Console.Error.WriteLineAsync(result.Error.ToString());
    return 1;
}

if (configuration["Migrator:AppLogin"] is { Length: > 0 } appLogin)
{
    await MigrationRunner.GrantSqlServerAppLoginAsync(connectionString, appLogin, CancellationToken.None);
}

// One-off move of accounts from placement's identity module; safe to repeat, existing rows are left alone.
if (configuration["Migrator:ImportFromPlacement"] is { Length: > 0 } placement)
{
    var imported = await ImportAsync(placement, connectionString, configuration["Migrator:Brand"] ?? "swiftbets");
    await Console.Out.WriteLineAsync($"imported {imported.Users} users and {imported.Tokens} refresh tokens from placement");
}

return 0;

static async Task<(int Users, int Tokens)> ImportAsync(string placementConnectionString, string identityConnectionString, string brand)
{
    var sql = SqlResources.For<Program>();
    await using var placement = new SqlConnection(placementConnectionString);
    await using var identity = new SqlConnection(identityConnectionString);
    var users = (await placement.QueryAsync<(Guid UserId, string Username, string PasswordHash, string Roles, DateTimeOffset CreatedAt)>(
        "SELECT UserId, Username, PasswordHash, Roles, CreatedAt FROM auth.Users")).ToList();
    foreach (var user in users)
    {
        await identity.ExecuteAsync(sql.Get("ImportUser"), new { user.UserId, user.Username, user.PasswordHash, user.Roles, user.CreatedAt, Brand = brand });
    }

    var tokens = (await placement.QueryAsync<(byte[] TokenHash, Guid UserId, Guid FamilyId, DateTimeOffset ExpiresAt, DateTimeOffset? ConsumedAt, DateTimeOffset? RevokedAt)>(
        "SELECT TokenHash, UserId, FamilyId, ExpiresAt, ConsumedAt, RevokedAt FROM auth.RefreshTokens WHERE ExpiresAt > SYSUTCDATETIME()")).ToList();
    foreach (var token in tokens)
    {
        await identity.ExecuteAsync(sql.Get("ImportRefreshToken"), new { token.TokenHash, token.UserId, token.FamilyId, token.ExpiresAt, token.ConsumedAt, token.RevokedAt });
    }

    return (users.Count, tokens.Count);
}

public partial class Program;
