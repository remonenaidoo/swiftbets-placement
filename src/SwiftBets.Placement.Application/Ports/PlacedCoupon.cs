using SwiftBets.Contracts.Placement;
using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Application.Ports;

public sealed record PlacedCoupon(Guid CouponId, Guid PunterId, BetType BetType, long Stake, string Currency, decimal TotalOdds, long PotentialPayout, IReadOnlyList<AcceptedLeg> Legs, DateTimeOffset PlacedAt);
