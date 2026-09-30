namespace SwiftBets.Placement.Domain.Coupons;

/// <summary>
/// Turns a requested coupon and the live prices into either an accepted coupon or the single reason it is refused.
/// Prices move: a leg is accepted at the live price when that is equal to or better than what the punter saw, and
/// refused (with the current prices) when it drifted against them.
/// </summary>
public static class CouponQuote
{
    public static (IReadOnlyList<AcceptedLeg>? Legs, PlacementRejection? Rejection) Evaluate(
        IReadOnlyList<LegSelection> selections, IReadOnlyDictionary<string, QuotedPrice> live, long stake, RiskLimits limits)
    {
        if (selections.Count == 0 || selections.Count > limits.MaxLegs)
        {
            return (null, new("invalid_leg_count", $"A coupon has 1 to {limits.MaxLegs} legs."));
        }

        if (selections.Select(s => s.FixtureId).Distinct(StringComparer.Ordinal).Count() != selections.Count)
        {
            return (null, new("correlated_legs", "An accumulator cannot contain two legs on the same fixture."));
        }

        if (stake < limits.MinStake || stake > limits.MaxStake)
        {
            return (null, new("stake_out_of_range", $"Stake must be between {limits.MinStake} and {limits.MaxStake} minor units."));
        }

        var legs = new List<AcceptedLeg>(selections.Count);
        var drifted = new List<QuotedPrice>();
        foreach (var selection in selections)
        {
            if (!live.TryGetValue(Key(selection.FixtureId, selection.MarketId, selection.SelectionId), out var price))
            {
                return (null, new("selection_not_found", $"{selection.SelectionId} is not on offer in {selection.MarketId}."));
            }

            if (!price.IsTradable)
            {
                return (null, new("market_suspended", $"{selection.MarketId} is not accepting bets."));
            }

            if (price.Odds < selection.RequestedOdds)
            {
                drifted.Add(price);
                continue;
            }

            legs.Add(new AcceptedLeg(Guid.CreateVersion7(), price.FixtureId, price.MarketId, price.SelectionId, price.Odds, price.OfferVersion));
        }

        if (drifted.Count > 0)
        {
            return (null, new("price_changed", "One or more prices moved against the coupon.", drifted));
        }

        return PayoutMath.Payout(stake, PayoutMath.TotalOdds(legs.Select(l => l.Odds))) > limits.MaxPotentialPayout
            ? (null, new("payout_too_high", $"Potential payout exceeds {limits.MaxPotentialPayout} minor units."))
            : (legs, null);
    }

    public static string Key(string fixtureId, string marketId, string selectionId) => $"{fixtureId}|{marketId}|{selectionId}";
}
