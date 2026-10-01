using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Testing;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace SwiftBets.Wallet.Migrator.Tests;

public sealed class MigratorTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Migrator_creates_the_schema_and_is_idempotent()
    {
        var connectionString = await sql.CreateDatabaseAsync("mig_" + Guid.NewGuid().ToString("N")[..10]);
        string[] args = [$"--ConnectionStrings:SbWallet={connectionString}"];

        (await RunAsync(args)).ShouldBe(0);
        (await RunAsync(args)).ShouldBe(0);

        await using var connection = new SqlConnection(connectionString);
        var tables = (await connection.QueryAsync<string>("SELECT s.name + '.' + t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id")).ToList();
        tables.ShouldContain("inbox.ProcessedMessages");
        tables.ShouldContain("outbox.Messages");
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.schemas WHERE name = 'wallet'")).ShouldBe(1);
    }

    [Fact]
    public async Task Reconciliation_migration_rolls_back_and_reapplies()
    {
        var connectionString = await sql.CreateDatabaseAsync("mig_" + Guid.NewGuid().ToString("N")[..10]);
        string[] args = [$"--ConnectionStrings:SbWallet={connectionString}"];
        (await RunAsync(args)).ShouldBe(0);
        await using var connection = new SqlConnection(connectionString);

        await connection.ExecuteAsync(Rollback("0004_reconciliation"));

        (await ReconciliationTablesAsync(connection)).ShouldBe(0);
        (await RunAsync(args)).ShouldBe(0);
        (await ReconciliationTablesAsync(connection)).ShouldBe(2);
    }

    [Fact]
    public async Task Missing_connection_string_fails_with_a_usage_code() =>
        (await RunAsync([])).ShouldBe(2);

    private static Task<int> ReconciliationTablesAsync(SqlConnection connection) =>
        connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('wallet') AND name LIKE 'Reconciliation%'");

    private static string Rollback(string migration)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream($"SwiftBets.Wallet.Migrator.Rollbacks.{migration}.sql")!;
        return new StreamReader(stream).ReadToEnd();
    }

    private static async Task<int> RunAsync(string[] args)
    {
        var entryPoint = typeof(Program).Assembly.EntryPoint!;
        var result = entryPoint.Invoke(null, [args]);
        return result is Task<int> task ? await task : (int)result!;
    }
}
