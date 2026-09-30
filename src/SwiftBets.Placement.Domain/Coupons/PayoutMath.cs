namespace SwiftBets.Placement.Domain.Coupons;

public static class PayoutMath
{
    /// <summary>Accumulator odds are the product of leg odds; payouts round down to the minor unit, never in the punter's favour by a fraction.</summary>
    public static decimal TotalOdds(IEnumerable<decimal> legOdds) => legOdds.Aggregate(1m, (total, odds) => total * odds);

    public static long Payout(long stake, decimal totalOdds) => (long)decimal.Floor(stake * totalOdds);
}
