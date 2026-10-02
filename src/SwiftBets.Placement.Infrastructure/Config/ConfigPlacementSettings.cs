using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Config;
using SwiftBets.Placement.Application.Ports;

namespace SwiftBets.Placement.Infrastructure.Config;

/// <summary>Reads the config service's compacted topic, held in memory; a key never set means no restriction.</summary>
public sealed class ConfigPlacementSettings(ICompactedState<ConfigEntryV1> state) : IPlacementSettings
{
    public bool IsStopped =>
        ConfigKeys.IsKillSwitchOn(Value(ConfigKeys.PlacementKillSwitch)) || ConfigKeys.ParseMode(Value(ConfigKeys.PlacementMode)) == PlacementMode.Closed;

    public bool IsPreMatchOnly => ConfigKeys.ParseMode(Value(ConfigKeys.PlacementMode)) == PlacementMode.PreMatchOnly;

    public int LiveDelaySeconds(string sport) => Seconds(Value(ConfigKeys.LiveDelaySeconds(sport))) ?? Seconds(Value(ConfigKeys.LiveDelayDefault)) ?? 0;

    public long? MaxStake(string currency) => ConfigKeys.ParseMinorUnits(Value(ConfigKeys.MaxStake(currency)));

    public long? MaxPayout(string currency) => ConfigKeys.ParseMinorUnits(Value(ConfigKeys.MaxPayout(currency)));

    public bool IsEnabled(string flag) => Value(ConfigKeys.Flag(flag)) == "true";

    private static int? Seconds(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds) ? Math.Min(seconds, 30) : null;

    private string? Value(string key) => state.TryGet(key, out var entry) ? entry.Value : null;
}
