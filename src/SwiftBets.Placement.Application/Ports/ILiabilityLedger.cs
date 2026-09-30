namespace SwiftBets.Placement.Application.Ports;

/// <summary>
/// Running potential payout per fixture. A soft limit: it is checked before the reserve and added after the coupon
/// commits, so concurrent placements can overshoot by a few coupons; the risk service tracks exact liability.
/// </summary>
public interface ILiabilityLedger
{
    Task<IReadOnlyDictionary<string, long>> GetAsync(IReadOnlyList<string> fixtureIds, CancellationToken cancellationToken);

    Task AddAsync(IReadOnlyList<string> fixtureIds, long potentialPayout);
}
