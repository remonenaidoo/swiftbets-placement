using Microsoft.Extensions.Configuration;
using SwiftBets.BuildingBlocks.Persistence;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
var connectionString = configuration["ConnectionStrings:SbHistory"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    await Console.Error.WriteLineAsync("ConnectionStrings:SbHistory is required.");
    return 2;
}

var result = MigrationRunner.RunPostgres(connectionString, configuration.GetValue("Migrator:EnsureDatabase", false), new MigrationSource(typeof(Program).Assembly, 1));
if (!result.Successful)
{
    await Console.Error.WriteLineAsync(result.Error.ToString());
    return 1;
}

return 0;

public partial class Program;
