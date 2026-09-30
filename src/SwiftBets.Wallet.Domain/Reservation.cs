namespace SwiftBets.Wallet.Domain;

public sealed class Reservation(Guid reservationId, Guid accountId, long amount, string currency, string reference, ReservationState state)
{
    public Guid ReservationId { get; } = reservationId;

    public Guid AccountId { get; } = accountId;

    public long Amount { get; } = amount;

    public string Currency { get; } = currency;

    public string Reference { get; } = reference;

    public ReservationState State { get; private set; } = state;

    public WalletFailure? MoveTo(ReservationState target)
    {
        if (State != ReservationState.Held)
        {
            return WalletFailure.InvalidState;
        }

        State = target;
        return null;
    }
}
