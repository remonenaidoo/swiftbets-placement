using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Application.Placing;

public sealed record PlaceCouponCommand(Guid PunterId, string IdempotencyKey, long Stake, string Currency, IReadOnlyList<LegSelection> Legs);
