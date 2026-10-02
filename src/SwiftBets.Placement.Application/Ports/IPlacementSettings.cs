namespace SwiftBets.Placement.Application.Ports;

/// <summary>Operational settings from the config service; anything not configured means no extra restriction.</summary>
public interface IPlacementSettings
{
    /// <summary>True while the kill switch is on or the placement mode is closed.</summary>
    bool IsStopped { get; }

    long? MaxStake(string currency);

    long? MaxPayout(string currency);

    /// <summary>True when the mode is pre-match only: bets on fixtures in play are refused.</summary>
    bool IsPreMatchOnly { get; }

    /// <summary>Seconds an in-play bet waits before it is re-priced and accepted: the sport's key, else the default, else none.</summary>
    int LiveDelaySeconds(string sport);

    /// <summary>A feature flag; off unless set to true.</summary>
    bool IsEnabled(string flag);
}
