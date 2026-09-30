using System.Text.Json;
using StackExchange.Redis;
using SwiftBets.Contracts.Offer;
using SwiftBets.Contracts.Serialization;
using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Infrastructure.Offer;

/// <summary>Reads the offer service's published Redis layout (<c>offer:fixture:{id}</c> holding a FixtureChangedV1 snapshot).</summary>
public sealed class RedisOfferReader(IConnectionMultiplexer redis, TimeProvider time) : IOfferReader
{
    public async Task<IReadOnlyDictionary<string, QuotedPrice>> QuoteAsync(IReadOnlyList<LegSelection> selections, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var fixtureIds = selections.Select(s => s.FixtureId).Distinct(StringComparer.Ordinal).ToList();
        var snapshots = await Task.WhenAll(fixtureIds.Select(id => db.HashGetAsync($"offer:fixture:{id}", "snapshot")));
        var now = time.GetUtcNow();
        var prices = new Dictionary<string, QuotedPrice>(StringComparer.Ordinal);
        foreach (var json in snapshots.Where(s => !s.IsNullOrEmpty))
        {
            var fixture = JsonSerializer.Deserialize<FixtureChangedV1>(json.ToString(), ContractJson.Options)!;
            foreach (var market in fixture.Markets)
            {
                var tradable = fixture.Status == FixtureStatus.Scheduled && market.Status == MarketStatus.Open && fixture.KickoffAt > now;
                foreach (var selection in market.Selections)
                {
                    prices[CouponQuote.Key(fixture.FixtureId, market.MarketId, selection.SelectionId)] =
                        new QuotedPrice(fixture.FixtureId, market.MarketId, selection.SelectionId, selection.Odds, fixture.OfferVersion, tradable);
                }
            }
        }

        return prices;
    }
}
