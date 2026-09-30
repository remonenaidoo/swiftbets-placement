using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Domain.Tests;

public sealed class CouponQuoteTests
{
    private static readonly Dictionary<string, QuotedPrice> Live = new()
    {
        [CouponQuote.Key("f1", "m1", "home")] = new("f1", "m1", "home", 2.10m, 5, true),
        [CouponQuote.Key("f2", "m2", "away")] = new("f2", "m2", "away", 1.50m, 9, true),
    };

    [Fact]
    public void Accumulator_is_accepted_at_the_live_price_when_it_improved()
    {
        var (legs, rejection) = CouponQuote.Evaluate([new("f1", "m1", "home", 2.00m, 4), new("f2", "m2", "away", 1.50m, 9)], Live, 1_000, RiskLimits.Default);

        rejection.ShouldBeNull();
        legs!.Select(l => l.Odds).ShouldBe([2.10m, 1.50m]);
        PayoutMath.Payout(1_000, PayoutMath.TotalOdds(legs!.Select(l => l.Odds))).ShouldBe(3_150);
    }

    [Fact]
    public void Coupon_is_refused_with_current_prices_when_a_price_drifted_against_it()
    {
        var (legs, rejection) = CouponQuote.Evaluate([new("f1", "m1", "home", 2.20m, 5)], Live, 1_000, RiskLimits.Default);

        legs.ShouldBeNull();
        rejection!.Code.ShouldBe("price_changed");
        rejection.CurrentPrices!.Single().Odds.ShouldBe(2.10m);
    }
}
