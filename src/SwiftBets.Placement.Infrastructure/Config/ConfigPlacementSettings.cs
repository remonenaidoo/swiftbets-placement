using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Config;
using SwiftBets.Placement.Application.Ports;

namespace SwiftBets.Placement.Infrastructure.Config;

/// <summary>Reads the config service's compacted topic, held in memory; a key never set means no restriction.</summary>
public sealed class ConfigPlacementSettings(ICompactedState<ConfigEntryV1> state) : IPlacementSettings
{
    public bool IsStopped =>
        ConfigKeys.IsKillSwitchOn(Value(ConfigKeys.PlacementKillSwitch)) || ConfigKeys.ParseMode(Value(ConfigKeys.PlacementMode)) == PlacementMode.Closed;

    public long? MaxStake(string currency) => ConfigKeys.ParseMinorUnits(Value(ConfigKeys.MaxStake(currency)));

    public long? MaxPayout(string currency) => ConfigKeys.ParseMinorUnits(Value(ConfigKeys.MaxPayout(currency)));

    private string? Value(string key) => state.TryGet(key, out var entry) ? entry.Value : null;
}
