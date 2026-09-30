using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ports;

public interface IWalletTransaction : IAsyncDisposable
{
    /// <summary>Looks the key up without locking; a concurrent duplicate is caught by the unique index on save.</summary>
    Task<StoredPosting?> FindPostingAsync(string idempotencyKey);

    Task<Account?> LockAccountAsync(Guid accountId);

    Task<Reservation?> LockReservationAsync(Guid reservationId);

    /// <summary>Opens an empty punter account if none exists and returns it locked.</summary>
    Task<Account> OpenPunterAccountAsync(Guid accountId, string currency);

    /// <exception cref="DuplicateIdempotencyKeyException">Another transaction committed the same key.</exception>
    Task SaveAsync(Posting posting, StoredPosting record, IReadOnlyList<Account> changedAccounts, Reservation? reservation, bool isNewReservation);

    Task CommitAsync();
}
