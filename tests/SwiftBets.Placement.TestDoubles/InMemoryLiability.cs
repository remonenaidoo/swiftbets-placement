using System.Collections.Concurrent;
using SwiftBets.Placement.Application.Ports;

namespace SwiftBets.Placement.TestDoubles;

public sealed class InMemoryLiability : ILiabilityLedger
{
    private readonly ConcurrentDictionary<string, long> _values = new(StringComparer.Ordinal);

    public Task<IReadOnlyDictionary<string, long>> GetAsync(IReadOnlyList<string> fixtureIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, long>>(fixtureIds.Where(_values.ContainsKey).ToDictionary(id => id, id => _values[id]));

    public Task AddAsync(IReadOnlyList<string> fixtureIds, long potentialPayout)
    {
        foreach (var id in fixtureIds)
        {
            _values.AddOrUpdate(id, potentialPayout, (_, v) => v + potentialPayout);
        }

        return Task.CompletedTask;
    }
}
