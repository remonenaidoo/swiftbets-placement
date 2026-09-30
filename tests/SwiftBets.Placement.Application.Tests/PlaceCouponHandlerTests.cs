using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Application.Sweeping;
using SwiftBets.Placement.Domain.Coupons;
using SwiftBets.Placement.Domain.Sagas;
using SwiftBets.Placement.TestDoubles;

namespace SwiftBets.Placement.Application.Tests;

public sealed class PlaceCouponHandlerTests
{
    private static readonly Guid Punter = Guid.NewGuid();

    [Fact]
    public async Task Coupon_is_placed_persisted_and_captured()
    {
        var (handler, store, wallet, _, _) = Build();

        var result = await handler.HandleAsync(Command("key-000001"), CancellationToken.None);

        result.Status.ShouldBe(201);
        store.Coupons.ShouldHaveSingleItem().PotentialPayout.ShouldBe(5_000);
        store.Intents.Values.Single().State.ShouldBe(SagaState.Completed);
        wallet.StateOf(store.Intents.Values.Single().ReservationId!.Value).ShouldBe("Captured");
    }

    [Fact]
    public async Task Wallet_outage_fails_fast_and_the_sweeper_leaves_nothing_held()
    {
        var (handler, store, wallet, clock, sweeper) = Build();
        wallet.IsDown = true;

        var result = await handler.HandleAsync(Command("key-000002"), CancellationToken.None);
        wallet.IsDown = false;
        clock.Advance(TimeSpan.FromMinutes(1));
        await sweeper.SweepAsync(CancellationToken.None);

        result.Status.ShouldBe(503);
        result.Body.ShouldContain("wallet_unavailable");
        store.Coupons.ShouldBeEmpty();
        store.Intents.Values.Single().State.ShouldBe(SagaState.Compensated);
        wallet.Balance.ShouldBe(100_000);
    }

    private static PlaceCouponCommand Command(string key) =>
        new(Punter, key, 2_500, "ZAR", [new LegSelection("fx-1", "fx-1-1x2", "home", 2.00m, 3)]);

    private static (PlaceCouponHandler Handler, InMemoryCouponStore Store, InMemoryWallet Wallet, FakeTimeProvider Clock, SweepOrphansHandler Sweeper) Build()
    {
        var store = new InMemoryCouponStore();
        var wallet = new InMemoryWallet();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new PlaceCouponHandler(store, new InMemoryLiability(), new StaticOffer(StaticOffer.Home), wallet, new ArmableFaults(), Options.Create(new PlacementOptions()), clock);
        return (handler, store, wallet, clock, new SweepOrphansHandler(store, wallet, clock, NullLogger<SweepOrphansHandler>.Instance));
    }
}
