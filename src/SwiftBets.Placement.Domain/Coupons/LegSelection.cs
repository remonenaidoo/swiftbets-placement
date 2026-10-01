namespace SwiftBets.Placement.Domain.Coupons;

/// <summary>What the punter asked for: a selection at the odds and offer version they saw.</summary>
public sealed record LegSelection(string FixtureId, string MarketId, string SelectionId, decimal RequestedOdds, long OfferVersion, bool IsBanker = false);
