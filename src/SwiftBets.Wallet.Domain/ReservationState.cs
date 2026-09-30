namespace SwiftBets.Wallet.Domain;

public enum ReservationState : byte
{
    Held = 1,
    Captured = 2,
    Released = 3,
}
