using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SwiftBets.Contracts.Placement;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Domain.Coupons;
using SwiftBets.Placement.TestDoubles;

namespace SwiftBets.Placement.Application.Tests;

public sealed class SystemBetTests
{
    private static readonly QuotedPrice[] Prices =
    [
        new("fx-1", "fx-1-1x2", "home", 2.00m, 3, true),
        new("fx-2", "fx-2-1x2", "away", 3.00m, 4, true),
        new("fx-3", "fx-3-1x2", "draw", 4.00m, 5, true),
        new("fx-4", "fx-4-1x2", "home", 1.50m, 6, true),
    ];

    [Fact]
    public async Task System_bets_stay_closed_until_the_flag_is_on()
    {
        var (handler, store, wallet, settings) = Build();

        var refused = await handler.HandleAsync(Trixie("key-sys-001"), CancellationToken.None);
        settings.Flags.Add(PlaceCouponHandler.SystemBetsFlag);
        var placed = await handler.HandleAsync(Trixie("key-sys-002"), CancellationToken.None);

        refused.Body.ShouldContain("system_bets_unavailable");
        placed.Status.ShouldBe(201);
        placed.Body.ShouldContain("\"lines\":4");
        var coupon = store.Coupons.Single();
        coupon.BetType.ShouldBe(BetType.System);
        coupon.Stake.ShouldBe(400);
        coupon.PotentialPayout.ShouldBe(600 + 800 + 1_200 + 2_400);
        store.PlacedV1.ShouldBe([null]);
        store.PlacedV2.Single().Bets.Single().Folds.ShouldBe([2, 3]);
        wallet.Balance.ShouldBe(100_000 - 400);
    }

    [Fact]
    public async Task A_banker_trixie_puts_the_banker_in_every_line()
    {
        var (handler, store, _, settings) = Build();
        settings.Flags.Add(PlaceCouponHandler.SystemBetsFlag);
        var command = new PlaceCouponCommand(Guid.NewGuid(), "key-sys-003", 400, "ZAR",
            [Leg(Prices[3], banker: true), Leg(Prices[0]), Leg(Prices[1]), Leg(Prices[2])], [new BetRequest("trixie", [2, 3], 100)]);

        (await handler.HandleAsync(command, CancellationToken.None)).Status.ShouldBe(201);

        store.Coupons.Single().PotentialPayout.ShouldBe((long)((600 + 800 + 1_200 + 2_400) * 1.5m));
        store.PlacedV2.Single().Legs.Count(l => l.IsBanker).ShouldBe(1);
    }

    [Fact]
    public async Task The_stake_must_be_what_the_bets_add_up_to()
    {
        var (handler, _, wallet, settings) = Build();
        settings.Flags.Add(PlaceCouponHandler.SystemBetsFlag);

        var result = await handler.HandleAsync(Trixie("key-sys-004") with { Stake = 100 }, CancellationToken.None);

        result.Body.ShouldContain("stake_mismatch");
        result.Body.ShouldContain("ZAR 4.00");
        wallet.Balance.ShouldBe(100_000);
    }

    [Fact]
    public async Task A_banker_without_a_bet_is_refused()
    {
        var (handler, _, _, _) = Build();

        var result = await handler.HandleAsync(new PlaceCouponCommand(Guid.NewGuid(), "key-sys-005", 100, "ZAR", [Leg(Prices[0], banker: true), Leg(Prices[1])]), CancellationToken.None);

        result.Body.ShouldContain("invalid_bets");
    }

    [Fact]
    public async Task An_old_style_accumulator_is_published_as_both_versions()
    {
        var (handler, store, _, _) = Build();

        var result = await handler.HandleAsync(new PlaceCouponCommand(Guid.NewGuid(), "key-sys-006", 1_000, "ZAR", [Leg(Prices[0]), Leg(Prices[1])]), CancellationToken.None);

        result.Status.ShouldBe(201);
        store.PlacedV1.Single()!.BetType.ShouldBe(BetType.Accumulator);
        store.PlacedV1.Single()!.PotentialPayout.MinorUnits.ShouldBe(6_000);
        var bet = store.PlacedV2.Single().Bets.Single();
        bet.ShouldSatisfyAllConditions(b => b.Name.ShouldBe("accumulator"), b => b.Folds.ShouldBe([2]), b => b.Lines.ShouldBe(1));
    }

    [Fact]
    public async Task Bad_folds_and_overlarge_payouts_are_refused_before_funds_are_held()
    {
        var (handler, _, wallet, settings) = Build();
        settings.Flags.Add(PlaceCouponHandler.SystemBetsFlag);

        var folds = await handler.HandleAsync(Trixie("key-sys-007") with { Bets = [new BetRequest("x", [5], 100)] }, CancellationToken.None);
        settings.Payouts["ZAR"] = 1_000;
        var capped = await handler.HandleAsync(Trixie("key-sys-009"), CancellationToken.None);

        folds.Body.ShouldContain("invalid_bets");
        capped.Body.ShouldContain("payout_above_limit");
        wallet.Balance.ShouldBe(100_000);
    }

    private static LegSelection Leg(QuotedPrice p, bool banker = false) => new(p.FixtureId, p.MarketId, p.SelectionId, p.Odds, p.OfferVersion, banker);

    private static PlaceCouponCommand Trixie(string key) =>
        new(Guid.NewGuid(), key, 400, "ZAR", [Leg(Prices[0]), Leg(Prices[1]), Leg(Prices[2])], [new BetRequest("trixie", [2, 3], 100)]);

    private static (PlaceCouponHandler Handler, InMemoryCouponStore Store, InMemoryWallet Wallet, StaticSettings Settings) Build()
    {
        var store = new InMemoryCouponStore();
        var wallet = new InMemoryWallet();
        var settings = new StaticSettings();
        var handler = new PlaceCouponHandler(store, new InMemoryLiability(), new StaticOffer(Prices), wallet, settings, new ArmableFaults(),
            Options.Create(new PlacementOptions()), new FakeTimeProvider(DateTimeOffset.UtcNow));
        return (handler, store, wallet, settings);
    }
}
