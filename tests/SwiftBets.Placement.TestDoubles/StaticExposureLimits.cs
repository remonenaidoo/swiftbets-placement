using SwiftBets.Placement.Application.Ports;

namespace SwiftBets.Placement.TestDoubles;

public sealed class StaticExposureLimits : IExposureLimits
{
    public HashSet<string> Suspended { get; } = new(StringComparer.Ordinal);

    public bool IsSuspended(string fixtureId) => Suspended.Contains(fixtureId);
}
