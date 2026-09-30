using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Application.Ports;

public interface IOfferReader
{
    /// <summary>Live prices keyed by <see cref="CouponQuote.Key"/>; selections not on offer are simply absent.</summary>
    Task<IReadOnlyDictionary<string, QuotedPrice>> QuoteAsync(IReadOnlyList<LegSelection> selections, CancellationToken cancellationToken);
}
