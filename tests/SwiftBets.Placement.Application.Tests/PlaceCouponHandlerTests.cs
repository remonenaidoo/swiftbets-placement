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

    [Fact]
    public async Task A_stopped_placement_refuses_before_anything_is_held_and_resumes_when_lifted()
    {
        var settings = new StaticSettings { IsStopped = true };
        var (handler, store, wallet, _, _) = Build(settings);

        var refused = await handler.HandleAsync(Command("key-000003"), CancellationToken.None);
        settings.IsStopped = false;
        var placed = await handler.HandleAsync(Command("key-000004"), CancellationToken.None);

        refused.Status.ShouldBe(422);
        refused.Body.ShouldContain("placement_suspended");
        placed.Status.ShouldBe(201);
        store.Coupons.Count.ShouldBe(1);
        wallet.Balance.ShouldBe(100_000 - 2_500);
    }

    [Fact]
    public async Task A_fixture_risk_has_suspended_refuses_new_coupons_before_anything_is_held()
    {
        var exposure = new StaticExposureLimits { Suspended = { "fx-1" } };
        var (handler, store, wallet, _, _) = Build(exposure: exposure);

        var refused = await handler.HandleAsync(Command("key-000021"), CancellationToken.None);

        refused.Status.ShouldBe(422);
        refused.Body.ShouldContain("fixture_exposure_capped");
        store.Coupons.ShouldBeEmpty();
        wallet.Balance.ShouldBe(100_000);
    }

    [Fact]
    public async Task A_suspension_on_another_fixture_does_not_touch_this_one()
    {
        var exposure = new StaticExposureLimits { Suspended = { "fx-other" } };
        var (handler, _, _, _, _) = Build(exposure: exposure);

        (await handler.HandleAsync(Command("key-000022"), CancellationToken.None)).Status.ShouldBe(201);
    }

    [Fact]
    public async Task Stake_and_payout_limits_refuse_in_the_coupon_currency_only()
    {
        var settings = new StaticSettings();
        settings.Stakes["ZAR"] = 2_000;
        var (handler, store, _, _, _) = Build(settings);

        var stake = await handler.HandleAsync(Command("key-000005"), CancellationToken.None);
        settings.Stakes.Clear();
        settings.Payouts["ZAR"] = 4_999;
        var payout = await handler.HandleAsync(Command("key-000006"), CancellationToken.None);
        settings.Payouts.Clear();
        settings.Payouts["USD"] = 1;
        var other = await handler.HandleAsync(Command("key-000007"), CancellationToken.None);

        stake.Body.ShouldContain("stake_above_limit");
        stake.Body.ShouldContain("ZAR 20.00");
        payout.Body.ShouldContain("payout_above_limit");
        other.Status.ShouldBe(201);
        store.Coupons.Count.ShouldBe(1);
    }

    private static PlaceCouponCommand Command(string key) =>
        new(Punter, key, 2_500, "ZAR", [new LegSelection("fx-1", "fx-1-1x2", "home", 2.00m, 3)]);

    private static (PlaceCouponHandler Handler, InMemoryCouponStore Store, InMemoryWallet Wallet, FakeTimeProvider Clock, SweepOrphansHandler Sweeper) Build(StaticSettings? settings = null, StaticExposureLimits? exposure = null)
    {
        var store = new InMemoryCouponStore();
        var wallet = new InMemoryWallet();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new PlaceCouponHandler(store, new InMemoryLiability(), exposure ?? new StaticExposureLimits(), new StaticOffer(StaticOffer.Home), wallet, settings ?? new StaticSettings(), new ArmableFaults(), Options.Create(new PlacementOptions()), clock);
        return (handler, store, wallet, clock, new SweepOrphansHandler(store, wallet, clock, NullLogger<SweepOrphansHandler>.Instance));
    }
}
