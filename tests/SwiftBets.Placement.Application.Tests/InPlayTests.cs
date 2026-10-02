using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Coupons;
using SwiftBets.Placement.TestDoubles;

namespace SwiftBets.Placement.Application.Tests;

public sealed class InPlayTests
{
    private static readonly QuotedPrice LiveHome = StaticOffer.Home with { IsLive = true };

    [Fact]
    public async Task An_in_play_coupon_waits_the_live_delay_and_is_accepted_at_the_refreshed_price()
    {
        var offer = new SwitchableOffer(LiveHome);
        var (handler, clock) = Build(offer, new StaticSettings { LiveDelay = 5 });

        var placing = handler.HandleAsync(Command("live-0001"), CancellationToken.None);
        placing.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(5));

        (await placing).Status.ShouldBe(201);
    }

    [Fact]
    public async Task A_market_suspended_during_the_live_delay_refuses_the_coupon()
    {
        var offer = new SwitchableOffer(LiveHome);
        var (handler, clock) = Build(offer, new StaticSettings { LiveDelay = 5 });

        var placing = handler.HandleAsync(Command("live-0002"), CancellationToken.None);
        offer.Price = LiveHome with { IsTradable = false };
        clock.Advance(TimeSpan.FromSeconds(5));

        var result = await placing;
        (result.Status, result.Body.Contains("market_suspended", StringComparison.Ordinal)).ShouldBe((422, true));
    }

    [Fact]
    public async Task Pre_match_only_refuses_an_in_play_leg()
    {
        var (handler, _) = Build(new SwitchableOffer(LiveHome), new StaticSettings { IsPreMatchOnly = true });

        (await handler.HandleAsync(Command("live-0003"), CancellationToken.None)).Body.ShouldContain("live_betting_unavailable");
    }

    [Fact]
    public async Task Refresh_reports_a_moved_price_and_a_leg_no_longer_on_offer()
    {
        var refresh = new RefreshCouponHandler(new SwitchableOffer(StaticOffer.Home with { Odds = 2.10m }));

        var legs = await refresh.HandleAsync(
            [new LegSelection("fx-1", "fx-1-1x2", "home", 2.00m, 0), new LegSelection("fx-9", "fx-9-1x2", "home", 2.00m, 0)], CancellationToken.None);

        (legs[0].Odds, legs[0].PriceChanged, legs[0].Tradable).ShouldBe((2.10m, true, true));
        (legs[1].OnOffer, legs[1].Tradable).ShouldBe((false, false));
    }

    private static PlaceCouponCommand Command(string key) =>
        new(Guid.NewGuid(), key, 2_500, "ZAR", [new LegSelection("fx-1", "fx-1-1x2", "home", 2.00m, 3)]);

    private static (PlaceCouponHandler, FakeTimeProvider) Build(IOfferReader offer, StaticSettings settings)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        return (new PlaceCouponHandler(new InMemoryCouponStore(), new InMemoryLiability(), new StaticExposureLimits(), offer, new InMemoryWallet(), settings, new ArmableFaults(), Options.Create(new PlacementOptions()), clock), clock);
    }

    private sealed class SwitchableOffer(QuotedPrice price) : IOfferReader
    {
        public QuotedPrice Price { get; set; } = price;

        public Task<IReadOnlyDictionary<string, QuotedPrice>> QuoteAsync(IReadOnlyList<LegSelection> selections, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, QuotedPrice>>(new Dictionary<string, QuotedPrice> { [CouponQuote.Key(Price.FixtureId, Price.MarketId, Price.SelectionId)] = Price });
    }
}
