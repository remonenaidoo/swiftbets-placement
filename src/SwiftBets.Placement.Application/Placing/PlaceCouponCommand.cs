using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Application.Placing;

/// <summary>Stake is the coupon's total. Without bets the coupon is one single or accumulator over all its legs.</summary>
public sealed record PlaceCouponCommand(Guid PunterId, string IdempotencyKey, long Stake, string Currency, IReadOnlyList<LegSelection> Legs, IReadOnlyList<BetRequest>? Bets = null);
