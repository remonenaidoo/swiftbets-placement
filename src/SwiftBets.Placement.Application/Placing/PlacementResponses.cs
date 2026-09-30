using System.Text.Json;
using SwiftBets.Contracts.Serialization;
using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Application.Placing;

public static class PlacementResponses
{
    public const int Placed = 201;
    public const int Refused = 422;
    public const int Conflict = 409;
    public const int Unavailable = 503;

    public static string Coupon(PlacedCoupon coupon) => JsonSerializer.Serialize(new
    {
        coupon.CouponId,
        status = "placed",
        coupon.BetType,
        stake = new { minorUnits = coupon.Stake, currency = coupon.Currency },
        coupon.TotalOdds,
        potentialPayout = new { minorUnits = coupon.PotentialPayout, currency = coupon.Currency },
        legs = coupon.Legs.Select(l => new { l.LegId, l.FixtureId, l.MarketId, l.SelectionId, l.Odds, l.OfferVersion }),
        coupon.PlacedAt,
    }, ContractJson.Options);

    public static string Error(Guid couponId, int status, string code, string message, IReadOnlyList<QuotedPrice>? currentPrices = null) => JsonSerializer.Serialize(new
    {
        type = $"https://swiftbets.dev/errors/{code}",
        title = status == Unavailable ? "Service Unavailable" : status == Conflict ? "Conflict" : "Unprocessable Entity",
        status,
        code,
        detail = message,
        couponId,
        currentPrices = currentPrices?.Select(p => new { p.FixtureId, p.MarketId, p.SelectionId, p.Odds, p.OfferVersion }),
    }, ContractJson.Options);
}
