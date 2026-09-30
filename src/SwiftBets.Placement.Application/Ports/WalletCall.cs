namespace SwiftBets.Placement.Application.Ports;

/// <summary>A wallet reply: refused carries the wallet's failure code; unavailable means the outcome is unknown and must be reconciled.</summary>
public sealed record WalletCall(WalletCallStatus Status, Guid? ReservationId = null, string? ReservationState = null, string? FailureCode = null, long? Available = null)
{
    public static WalletCall Unavailable { get; } = new(WalletCallStatus.Unavailable);
}
