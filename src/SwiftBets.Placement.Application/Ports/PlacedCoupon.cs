using SwiftBets.Contracts.Placement;
using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Application.Ports;

/// <summary>TotalOdds is the accumulator price for a single or accumulator, and potential payout over stake for a system coupon.</summary>
public sealed record PlacedCoupon(
    Guid CouponId, Guid PunterId, BetType BetType, long Stake, string Currency, decimal TotalOdds, long PotentialPayout, IReadOnlyList<AcceptedLeg> Legs, DateTimeOffset PlacedAt,
    IReadOnlyList<PricedBet> Bets);
