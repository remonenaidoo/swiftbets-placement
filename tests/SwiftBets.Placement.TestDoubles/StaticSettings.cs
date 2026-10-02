using SwiftBets.Placement.Application.Ports;

namespace SwiftBets.Placement.TestDoubles;

/// <summary>Settings a test sets directly; empty means nothing configured.</summary>
public sealed class StaticSettings : IPlacementSettings
{
    public bool IsStopped { get; set; }

    public bool IsPreMatchOnly { get; set; }

    public int LiveDelay { get; set; }

    public int LiveDelaySeconds(string sport) => LiveDelay;

    public Dictionary<string, long> Stakes { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, long> Payouts { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Flags { get; } = new(StringComparer.Ordinal);

    public bool IsEnabled(string flag) => Flags.Contains(flag);

    public long? MaxStake(string currency) => Stakes.TryGetValue(currency, out var v) ? v : null;

    public long? MaxPayout(string currency) => Payouts.TryGetValue(currency, out var v) ? v : null;
}
