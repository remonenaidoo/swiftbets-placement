namespace SwiftBets.Placement.Application.Ports;

/// <summary>Operational settings from the config service; anything not configured means no extra restriction.</summary>
public interface IPlacementSettings
{
    /// <summary>True while the kill switch is on or the placement mode is closed.</summary>
    bool IsStopped { get; }

    long? MaxStake(string currency);

    long? MaxPayout(string currency);

    /// <summary>A feature flag; off unless set to true.</summary>
    bool IsEnabled(string flag);
}
