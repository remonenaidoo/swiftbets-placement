using SwiftBets.Contracts.Placement;

namespace SwiftBets.Placement.Domain.Coupons;

/// <summary>A bet as asked for: fold sizes over the coupon's non-banker legs, and the stake per line.</summary>
public sealed record BetRequest(string Name, IReadOnlyList<int> Folds, long UnitStake);

public sealed record PricedBet(Guid BetId, string Name, IReadOnlyList<int> Folds, int Lines, long UnitStake, long Stake, long PotentialPayout);

/// <summary>
/// Prices bets over accepted legs with the same lines settlement will pay (<see cref="SystemBets"/>). Every line is the
/// chosen non-banker legs plus every banker; a line pays its unit stake times the product of its odds, rounded down.
/// </summary>
public static class BetPricing
{
    public const int MaxLines = 2_000;

    public static (IReadOnlyList<PricedBet>? Bets, PlacementRejection? Rejection) Price(IReadOnlyList<AcceptedLeg> legs, IReadOnlyList<BetRequest> bets)
    {
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(bets);
        if (bets.Count == 0)
        {
            return (null, new("invalid_bets", "A coupon needs at least one bet."));
        }

        var bankers = legs.Where(l => l.IsBanker).ToList();
        var others = legs.Where(l => !l.IsBanker).ToList();
        if (bankers.Count > 0 && others.Count == 0)
        {
            return (null, new("invalid_bets", "Bankers need at least one other selection."));
        }

        var bankerOdds = PayoutMath.TotalOdds(bankers.Select(l => l.Odds));
        var priced = new List<PricedBet>(bets.Count);
        foreach (var bet in bets)
        {
            if (SystemBets.Validate(others.Count, bet.Folds) is { } problem)
            {
                return (null, new("invalid_bets", problem));
            }

            if (bet.UnitStake <= 0)
            {
                return (null, new("invalid_bets", "Each line needs a stake above zero."));
            }

            var lines = SystemBets.Lines(others.Count, bet.Folds);
            if (lines > MaxLines)
            {
                return (null, new("too_many_lines", $"A bet can have at most {MaxLines} lines."));
            }

            var payout = SystemBets.Combinations(others.Count, bet.Folds)
                .Sum(line => PayoutMath.Payout(bet.UnitStake, bankerOdds * PayoutMath.TotalOdds(line.Select(i => others[i].Odds))));
            priced.Add(new PricedBet(Guid.CreateVersion7(), bet.Name, bet.Folds, lines, bet.UnitStake, checked(bet.UnitStake * lines), payout));
        }

        return (priced, null);
    }

    /// <summary>The one bet an old-style coupon is: all legs in one line, the whole stake on it.</summary>
    public static BetRequest Accumulator(int legs, long stake) => new(legs == 1 ? "single" : "accumulator", [legs], stake);
}
