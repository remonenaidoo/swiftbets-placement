using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ledger;

public sealed record WalletOutcome(bool WasApplied, WalletFailure? Failure, Guid? PostingId, Account? Account, Reservation? Reservation)
{
    public static WalletOutcome Failed(WalletFailure failure) => new(false, failure, null, null, null);
}
