namespace SwiftBets.Placement.Application.Ports;

/// <summary>The risk service's exposure rules, read locally from a compacted topic; placement never calls risk (D88).</summary>
public interface IExposureLimits
{
    /// <summary>True when the fixture has reached its exposure cap, or a trader suspended it, and takes no new coupons.</summary>
    bool IsSuspended(string fixtureId);
}
