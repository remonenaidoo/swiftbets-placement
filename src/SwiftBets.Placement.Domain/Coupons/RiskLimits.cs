namespace SwiftBets.Placement.Domain.Coupons;

public sealed record RiskLimits(long MinStake, long MaxStake, long MaxPotentialPayout, int MaxLegs, long MaxFixtureLiability)
{
    public static RiskLimits Default { get; } = new(MinStake: 100, MaxStake: 500_000, MaxPotentialPayout: 25_000_000, MaxLegs: 20, MaxFixtureLiability: 100_000_000);
}
