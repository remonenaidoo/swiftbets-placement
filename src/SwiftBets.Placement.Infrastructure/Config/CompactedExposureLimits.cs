using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Risk;
using SwiftBets.Placement.Application.Ports;

namespace SwiftBets.Placement.Infrastructure.Config;

/// <summary>Risk's compacted exposure-limits topic held in memory, keyed by fixture; a fixture never published is open.</summary>
public sealed class CompactedExposureLimits(ICompactedState<ExposureLimitV1> state) : IExposureLimits
{
    public bool IsSuspended(string fixtureId) => state.TryGet(fixtureId, out var limit) && limit.Suspended;
}
