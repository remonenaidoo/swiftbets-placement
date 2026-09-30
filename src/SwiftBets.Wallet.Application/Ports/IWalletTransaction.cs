using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ports;

public interface IWalletTransaction : IAsyncDisposable
{
    /// <summary>Looks the key up and locks its range, so concurrent callers with the same key queue behind this one.</summary>
    Task<StoredPosting?> FindPostingAsync(string idempotencyKey);

    Task<Account?> LockAccountAsync(Guid accountId);

    Task<Reservation?> LockReservationAsync(Guid reservationId);

    Task SaveAsync(Posting posting, StoredPosting record, IReadOnlyList<Account> changedAccounts, Reservation? reservation, bool isNewReservation);

    Task CommitAsync();
}
