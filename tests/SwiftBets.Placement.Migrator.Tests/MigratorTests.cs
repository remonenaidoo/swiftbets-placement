using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Testing;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace SwiftBets.Placement.Migrator.Tests;

public sealed class MigratorTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Migrator_creates_the_schema_and_is_idempotent()
    {
        var connectionString = await sql.CreateDatabaseAsync("mig_" + Guid.NewGuid().ToString("N")[..10]);
        string[] args = [$"--ConnectionStrings:SbPlacement={connectionString}"];

        (await RunAsync(args)).ShouldBe(0);
        (await RunAsync(args)).ShouldBe(0);

        await using var connection = new SqlConnection(connectionString);
        var tables = (await connection.QueryAsync<string>("SELECT s.name + '.' + t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id")).ToList();
        tables.ShouldContain("inbox.ProcessedMessages");
        tables.ShouldContain("outbox.Messages");
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.schemas WHERE name = 'placement'")).ShouldBe(1);
    }

    [Fact]
    public async Task Bets_roll_back_and_reapply_and_coupons_from_before_become_one_bet_each()
    {
        var connectionString = await sql.CreateDatabaseAsync("mig_" + Guid.NewGuid().ToString("N")[..10]);
        string[] args = [$"--ConnectionStrings:SbPlacement={connectionString}"];
        (await RunAsync(args)).ShouldBe(0);
        await using var connection = new SqlConnection(connectionString);

        await connection.ExecuteAsync(Rollback("0003_bets_and_bankers"));
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name = 'CouponBets'")).ShouldBe(0);
        var couponId = Guid.NewGuid();
        await connection.ExecuteAsync("""
            INSERT INTO placement.SagaIntents (CouponId, PunterId, IdempotencyKey, RequestHash, Stake, Currency, State, CreatedAt, UpdatedAt, DeadlineAt)
            VALUES (@CouponId, NEWID(), 'key-00000001', REPLICATE('a', 64), 2500, 'ZAR', 5, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            INSERT INTO placement.Coupons (CouponId, PunterId, BetType, Stake, Currency, TotalOdds, PotentialPayout, PlacedAt)
            VALUES (@CouponId, NEWID(), 1, 2500, 'ZAR', 6.0, 15000, SYSDATETIMEOFFSET());
            INSERT INTO placement.CouponLegs (LegId, CouponId, FixtureId, MarketId, SelectionId, Odds, OfferVersion)
            VALUES (NEWID(), @CouponId, 'fx-1', 'fx-1-1x2', 'home', 2.0, 1), (NEWID(), @CouponId, 'fx-2', 'fx-2-1x2', 'away', 3.0, 1);
            """, new { CouponId = couponId });

        (await RunAsync(args)).ShouldBe(0);

        var bet = await connection.QuerySingleAsync<(string Name, string Folds, int Lines, long Stake, long PotentialPayout)>(
            "SELECT Name, Folds, Lines, Stake, PotentialPayout FROM placement.CouponBets WHERE CouponId = @CouponId", new { CouponId = couponId });
        bet.ShouldBe(("accumulator", "2", 1, 2500L, 15000L));
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM placement.CouponLegs WHERE IsBanker = 1")).ShouldBe(0);
    }

    [Fact]
    public async Task Missing_connection_string_fails_with_a_usage_code() =>
        (await RunAsync([])).ShouldBe(2);

    private static string Rollback(string migration)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream($"SwiftBets.Placement.Migrator.Rollbacks.{migration}.sql")!;
        return new StreamReader(stream).ReadToEnd();
    }

    private static async Task<int> RunAsync(string[] args)
    {
        var entryPoint = typeof(Program).Assembly.EntryPoint!;
        var result = entryPoint.Invoke(null, [args]);
        return result is Task<int> task ? await task : (int)result!;
    }
}
