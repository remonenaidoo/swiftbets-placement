using StackExchange.Redis;
using SwiftBets.Placement.Application.Ports;

namespace SwiftBets.Placement.Infrastructure.Persistence;

/// <summary>One Redis hash of fixture to liability; HINCRBY keeps concurrent additions exact without a hot SQL row.</summary>
public sealed class RedisLiabilityLedger(IConnectionMultiplexer redis) : ILiabilityLedger
{
    private const string Key = "placement:liability";

    public async Task<IReadOnlyDictionary<string, long>> GetAsync(IReadOnlyList<string> fixtureIds, CancellationToken cancellationToken)
    {
        var values = await redis.GetDatabase().HashGetAsync(Key, [.. fixtureIds.Select(id => (RedisValue)id)]);
        return fixtureIds.Zip(values).Where(p => p.Second.HasValue).ToDictionary(p => p.First, p => (long)p.Second, StringComparer.Ordinal);
    }

    public async Task AddAsync(IReadOnlyList<string> fixtureIds, long potentialPayout)
    {
        var db = redis.GetDatabase();
        await Task.WhenAll(fixtureIds.Select(id => db.HashIncrementAsync(Key, id, potentialPayout)));
    }
}
