namespace SwiftBets.Placement.Domain.Coupons;

/// <summary>The live offer for one selection at placement time.</summary>
public sealed record QuotedPrice(string FixtureId, string MarketId, string SelectionId, decimal Odds, long OfferVersion, bool IsTradable);
