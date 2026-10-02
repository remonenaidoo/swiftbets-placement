using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Domain.Coupons;
using SwiftBets.Placement.Infrastructure.Persistence;
using SwiftBets.Placement.TestDoubles;

namespace SwiftBets.Placement.Infrastructure.Tests;

public sealed class CouponDigestTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_coupon_placed_in_the_window_is_in_the_digest_with_its_money_facts()
    {
        var (store, placedAt) = await PlaceOneAsync();

        var digest = await store.DigestAsync(placedAt.AddMinutes(-1), placedAt.AddMinutes(1), 5000, CancellationToken.None);

        var row = digest.ShouldHaveSingleItem();
        (row.Stake, row.PotentialPayout, row.Currency).ShouldBe((2_500L, 5_000L, "ZAR"));
    }

    [Fact]
    public async Task A_coupon_placed_outside_the_window_is_left_out()
    {
        var (store, placedAt) = await PlaceOneAsync();

        (await store.DigestAsync(placedAt.AddMinutes(1), placedAt.AddMinutes(2), 5000, CancellationToken.None)).ShouldBeEmpty();
    }

    private async Task<(SqlCouponStore Store, DateTimeOffset PlacedAt)> PlaceOneAsync()
    {
        var connectionString = await sql.CreateDatabaseAsync("placement_" + Guid.NewGuid().ToString("N")[..10]);
        var entry = typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { $"--ConnectionStrings:SbPlacement={connectionString}" }]);
        (entry is Task<int> task ? await task : (int)entry!).ShouldBe(0);

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var kafka = Options.Create(new KafkaOptions { BootstrapServers = "unused:9092", Environment = "test", ClientId = "test" });
        var store = new SqlCouponStore(new SqlServerConnectionFactory(connectionString), new SqlServerOutbox(kafka, clock), clock);
        var handler = new PlaceCouponHandler(store, new InMemoryLiability(), new StaticExposureLimits(), new StaticOffer(StaticOffer.Home), new InMemoryWallet(), new StaticSettings(), new ArmableFaults(), Options.Create(new PlacementOptions()), clock);
        var result = await handler.HandleAsync(new PlaceCouponCommand(Guid.NewGuid(), "digest-0001", 2_500, "ZAR", [new LegSelection("fx-1", "fx-1-1x2", "home", 2.00m, 3)]), CancellationToken.None);
        result.Status.ShouldBe(201);
        return (store, clock.GetUtcNow());
    }
}
