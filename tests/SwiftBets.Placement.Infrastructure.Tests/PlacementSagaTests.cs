using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Application.Sweeping;
using SwiftBets.Placement.Domain.Coupons;
using SwiftBets.Placement.Infrastructure.Persistence;
using SwiftBets.Placement.TestDoubles;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace SwiftBets.Placement.Infrastructure.Tests;

/// <summary>The Phase 1 gate scenarios against real SQL Server: a crash mid-saga is compensated, and a duplicated request debits once.</summary>
public sealed class PlacementSagaTests(SqlServerFixture sql)
{
    private static readonly Guid Punter = Guid.NewGuid();

    [Fact]
    public async Task Crash_after_reserve_is_compensated_by_the_sweeper()
    {
        var (handler, sweeper, wallet, faults, clock, connectionString) = await BuildAsync();
        faults.Armed = PlaceCouponHandler.FaultAfterReserve;

        await Should.ThrowAsync<FaultInjectedException>(() => handler.HandleAsync(Command("crash-key-1"), CancellationToken.None));
        wallet.Balance.ShouldBe(97_500);
        clock.Advance(TimeSpan.FromMinutes(1));
        (await sweeper.SweepAsync(CancellationToken.None)).ShouldBe(1);

        wallet.Balance.ShouldBe(100_000);
        await using var connection = new SqlConnection(connectionString);
        (await connection.ExecuteScalarAsync<byte>("SELECT State FROM placement.SagaIntents")).ShouldBe((byte)5);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM placement.Coupons")).ShouldBe(0);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox.Messages")).ShouldBe(0);
    }

    [Fact]
    public async Task Concurrent_duplicates_of_one_request_place_one_coupon_and_reserve_once()
    {
        var (handler, _, wallet, _, _, connectionString) = await BuildAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => handler.HandleAsync(Command("dup-key-001"), CancellationToken.None))));

        wallet.AppliedReserves.ShouldBe(1);
        results.Count(r => r.Status == 201 && !r.Replayed).ShouldBe(1);
        results.ShouldAllBe(r => r.Status == 201 || (r.Status == 409 && r.Body.Contains("placement_in_progress")));
        await using var connection = new SqlConnection(connectionString);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM placement.Coupons")).ShouldBe(1);
        // One coupon, published once and only as V2: no row on the retired V1 topic.
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox.Messages WHERE EventType = 'placement.coupon-placed' AND Topic LIKE '%.coupon-placed.v2.%'")).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox.Messages WHERE EventType = 'placement.coupon-placed' AND Topic LIKE '%.coupon-placed.v1.%'")).ShouldBe(0);
    }

    private static PlaceCouponCommand Command(string key) => new(Punter, key, 2_500, "ZAR", [new LegSelection("fx-1", "fx-1-1x2", "home", 2.00m, 3)]);

    private async Task<(PlaceCouponHandler, SweepOrphansHandler, InMemoryWallet, ArmableFaults, FakeTimeProvider, string)> BuildAsync()
    {
        var connectionString = await sql.CreateDatabaseAsync("placement_" + Guid.NewGuid().ToString("N")[..10]);
        var entry = typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { $"--ConnectionStrings:SbPlacement={connectionString}" }]);
        (entry is Task<int> task ? await task : (int)entry!).ShouldBe(0);

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var kafka = Options.Create(new KafkaOptions { BootstrapServers = "unused:9092", Environment = "test", ClientId = "test" });
        var store = new SqlCouponStore(new SqlServerConnectionFactory(connectionString), new SqlServerOutbox(kafka, clock), clock);
        var wallet = new InMemoryWallet();
        var faults = new ArmableFaults();
        var handler = new PlaceCouponHandler(store, new InMemoryLiability(), new StaticOffer(StaticOffer.Home), wallet, new StaticSettings(), faults, Options.Create(new PlacementOptions()), clock);
        return (handler, new SweepOrphansHandler(store, wallet, clock, NullLogger<SweepOrphansHandler>.Instance), wallet, faults, clock, connectionString);
    }
}
