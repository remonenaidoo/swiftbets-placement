using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Application.Placing;

/// <summary>
/// The betslip's refresh: re-reads the live offer for each leg without placing anything, so a client can show moved
/// prices, suspensions and in-play legs before the punter commits.
/// </summary>
public sealed class RefreshCouponHandler(IOfferReader offer)
{
    public sealed record LegState(string FixtureId, string MarketId, string SelectionId, decimal? Odds, long? OfferVersion, bool OnOffer, bool Tradable, bool Live, bool PriceChanged);

    public async Task<IReadOnlyList<LegState>> HandleAsync(IReadOnlyList<LegSelection> legs, CancellationToken cancellationToken)
    {
        var quotes = await offer.QuoteAsync(legs, cancellationToken);
        return [.. legs.Select(l => quotes.TryGetValue(CouponQuote.Key(l.FixtureId, l.MarketId, l.SelectionId), out var q)
            ? new LegState(l.FixtureId, l.MarketId, l.SelectionId, q.Odds, q.OfferVersion, true, q.IsTradable, q.IsLive, q.Odds != l.RequestedOdds)
            : new LegState(l.FixtureId, l.MarketId, l.SelectionId, null, null, false, false, false, false))];
    }
}
