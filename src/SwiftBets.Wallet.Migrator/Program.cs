using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
var connectionString = configuration["ConnectionStrings:SbWallet"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    await Console.Error.WriteLineAsync("ConnectionStrings:SbWallet is required.");
    return 2;
}

var result = MigrationRunner.RunSqlServer(
    connectionString,
    configuration.GetValue("Migrator:EnsureDatabase", false),
    MigrationSource.Inbox, OutboxRegistration.Migrations, new MigrationSource(typeof(Program).Assembly, 1));
if (!result.Successful)
{
    await Console.Error.WriteLineAsync(result.Error.ToString());
    return 1;
}

if (configuration["Migrator:AppLogin"] is { Length: > 0 } appLogin)
{
    await MigrationRunner.GrantSqlServerAppLoginAsync(connectionString, appLogin, CancellationToken.None);
}

// Demo and local deployments only; production never sets it.
if (configuration.GetValue("Migrator:SeedDemo", false))
{
    await using var connection = new SqlConnection(connectionString);
    await connection.ExecuteAsync(SqlResources.For<Program>().Get("DemoSeed"));
}

return 0;

public partial class Program;
