namespace SwiftBets.Placement.Domain.Coupons;

public sealed record AcceptedLeg(Guid LegId, string FixtureId, string MarketId, string SelectionId, decimal Odds, long OfferVersion);
