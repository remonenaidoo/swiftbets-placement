namespace SwiftBets.Placement.Application.Ports;

/// <summary>A placed coupon's money facts, as bet history checks its copy against them.</summary>
public sealed record CouponDigest(Guid CouponId, long Stake, long PotentialPayout, string Currency, DateTimeOffset PlacedAt);
