using SwiftBets.Wallet.Application.Ports;
using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ledger;

/// <summary>
/// The one way money moves: in one transaction, look the idempotency key up, lock the account row, apply the movement
/// and store the balanced posting under its key. The key's unique index is the arbiter: if a concurrent request with
/// the same key commits first, this one rolls back and replays that result. No range locks, so unrelated keys never
/// queue behind each other. Writes are never cancellable; a token honoured between debit and credit would leave money
/// half-moved.
/// </summary>
public sealed class PostingRunner(IWalletStore store)
{
    public async Task<WalletOutcome> RunAsync(
        string idempotencyKey,
        PostingKind kind,
        Guid accountId,
        long amount,
        Func<IWalletTransaction, Task<(WalletFailure? Failure, Posting? Posting, IReadOnlyList<Account> Changed, Reservation? Reservation, bool IsNewReservation)>> apply)
    {
        try
        {
            return await AttemptAsync(idempotencyKey, kind, accountId, amount, apply);
        }
        catch (DuplicateIdempotencyKeyException)
        {
            return await AttemptAsync(idempotencyKey, kind, accountId, amount, apply);
        }
    }

    private async Task<WalletOutcome> AttemptAsync(
        string idempotencyKey,
        PostingKind kind,
        Guid accountId,
        long amount,
        Func<IWalletTransaction, Task<(WalletFailure? Failure, Posting? Posting, IReadOnlyList<Account> Changed, Reservation? Reservation, bool IsNewReservation)>> apply)
    {
        await using var transaction = await store.BeginAsync();
        var existing = await transaction.FindPostingAsync(idempotencyKey);
        if (existing is not null)
        {
            if (existing.Kind != kind || existing.AccountId != accountId || existing.Amount != amount)
            {
                return WalletOutcome.Failed(WalletFailure.IdempotencyConflict);
            }

            var account = await transaction.LockAccountAsync(existing.AccountId);
            var reservation = existing.ReservationId is { } id ? await transaction.LockReservationAsync(id) : null;
            return new WalletOutcome(false, null, existing.PostingId, account, reservation);
        }

        var result = await apply(transaction);
        if (result.Failure is { } failure)
        {
            return WalletOutcome.Failed(failure);
        }

        var posting = result.Posting!;
        await transaction.SaveAsync(
            posting,
            new StoredPosting(posting.PostingId, kind, accountId, amount, result.Reservation?.ReservationId),
            result.Changed,
            result.Reservation,
            result.IsNewReservation);
        await transaction.CommitAsync();
        return new WalletOutcome(true, null, posting.PostingId, result.Changed.FirstOrDefault(a => a.AccountId == accountId), result.Reservation);
    }
}
