namespace SwiftBets.Placement.Domain.Coupons;

public sealed record PlacementRejection(string Code, string Message, IReadOnlyList<QuotedPrice>? CurrentPrices = null);
