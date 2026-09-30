using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.TestDoubles;

public sealed class StaticOffer(params QuotedPrice[] prices) : IOfferReader
{
    public static readonly QuotedPrice Home = new("fx-1", "fx-1-1x2", "home", 2.00m, 3, true);

    public Task<IReadOnlyDictionary<string, QuotedPrice>> QuoteAsync(IReadOnlyList<LegSelection> selections, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, QuotedPrice>>(prices.ToDictionary(p => CouponQuote.Key(p.FixtureId, p.MarketId, p.SelectionId)));
}
