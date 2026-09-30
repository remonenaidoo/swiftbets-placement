using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Infrastructure.Persistence;

internal sealed record AccountRow(Guid AccountId, byte Kind, string Currency, long Available, long Reserved, bool IsBlacklisted)
{
    public Account ToDomain() => new(AccountId, (AccountKind)Kind, Currency, Available, Reserved, IsBlacklisted);
}

internal sealed record ReservationRow(Guid ReservationId, Guid AccountId, long Amount, string Currency, string Reference, byte State)
{
    public Reservation ToDomain() => new(ReservationId, AccountId, Amount, Currency, Reference, (ReservationState)State);
}

internal sealed record PostingRow(Guid PostingId, byte Kind, Guid AccountId, long Amount, Guid? ReservationId);
