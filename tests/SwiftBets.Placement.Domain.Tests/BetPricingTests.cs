using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Domain.Tests;

public sealed class BetPricingTests
{
    private static AcceptedLeg Leg(decimal odds, bool banker = false) => new(Guid.NewGuid(), $"fx-{Guid.NewGuid():N}", "m", "s", odds, 1, banker);

    [Fact]
    public void A_trixie_prices_three_doubles_and_a_treble()
    {
        var (bets, rejection) = BetPricing.Price([Leg(2m), Leg(3m), Leg(4m)], [new BetRequest("trixie", [2, 3], 100)]);

        rejection.ShouldBeNull();
        var bet = bets!.Single();
        bet.Lines.ShouldBe(4);
        bet.Stake.ShouldBe(400);
        bet.PotentialPayout.ShouldBe(600 + 800 + 1_200 + 2_400);
    }

    [Fact]
    public void A_banker_joins_every_line()
    {
        var (bets, _) = BetPricing.Price([Leg(1.5m, banker: true), Leg(2m), Leg(3m)], [new BetRequest("doubles", [1], 100)]);

        bets!.Single().Lines.ShouldBe(2);
        bets!.Single().PotentialPayout.ShouldBe(300 + 450);
    }

    [Fact]
    public void Payouts_round_down_per_line()
    {
        var (bets, _) = BetPricing.Price([Leg(1.333m), Leg(1.333m)], [new BetRequest("singles", [1], 1)]);

        bets!.Single().PotentialPayout.ShouldBe(2);
    }

    [Fact]
    public void An_accumulator_is_one_line_of_every_leg()
    {
        var request = BetPricing.Accumulator(3, 1_000);
        var (bets, _) = BetPricing.Price([Leg(2m), Leg(2m), Leg(2m)], [request]);

        request.Name.ShouldBe("accumulator");
        BetPricing.Accumulator(1, 100).Name.ShouldBe("single");
        bets!.Single().ShouldSatisfyAllConditions(b => b.Lines.ShouldBe(1), b => b.PotentialPayout.ShouldBe(8_000));
    }

    [Theory]
    [InlineData(new[] { 4 }, 100, "invalid_bets")]
    [InlineData(new int[0], 100, "invalid_bets")]
    [InlineData(new[] { 1 }, 0, "invalid_bets")]
    public void Bad_bets_are_refused(int[] folds, long unit, string code) =>
        BetPricing.Price([Leg(2m), Leg(3m), Leg(4m)], [new BetRequest("x", folds, unit)]).Rejection!.Code.ShouldBe(code);

    [Fact]
    public void Bets_need_at_least_one_bet_and_a_non_banker()
    {
        BetPricing.Price([Leg(2m)], []).Rejection!.Code.ShouldBe("invalid_bets");
        BetPricing.Price([Leg(2m, banker: true)], [new BetRequest("x", [1], 100)]).Rejection!.Code.ShouldBe("invalid_bets");
    }

    [Fact]
    public void Too_many_lines_are_refused()
    {
        var legs = Enumerable.Range(0, 14).Select(_ => Leg(1.5m)).ToList();

        BetPricing.Price(legs, [new BetRequest("full cover", [.. Enumerable.Range(1, 14)], 1)]).Rejection!.Code.ShouldBe("too_many_lines");
    }

    [Fact]
    public void A_payout_above_the_platform_limit_is_refused()
    {
        CouponQuote.CheckPayout(RiskLimits.Default.MaxPotentialPayout, RiskLimits.Default).ShouldBeNull();
        CouponQuote.CheckPayout(RiskLimits.Default.MaxPotentialPayout + 1, RiskLimits.Default)!.Code.ShouldBe("payout_too_high");
    }
}
