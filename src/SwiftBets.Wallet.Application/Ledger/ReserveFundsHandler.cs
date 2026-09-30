using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ledger;

public sealed class ReserveFundsHandler(PostingRunner runner)
{
    public Task<WalletOutcome> HandleAsync(string idempotencyKey, Guid accountId, long amount, string currency, string reference) =>
        runner.RunAsync(idempotencyKey, PostingKind.Reserve, accountId, amount, async transaction =>
        {
            var account = await transaction.LockAccountAsync(accountId);
            if (account is null)
            {
                return (WalletFailure.AccountNotFound, null, [], null, false);
            }

            if (account.Reserve(amount, currency) is { } failure)
            {
                return (failure, null, [], null, false);
            }

            var reservation = new Reservation(Guid.CreateVersion7(), accountId, amount, currency, reference, ReservationState.Held);
            var posting = new Posting(Guid.CreateVersion7(), PostingKind.Reserve, idempotencyKey, reference,
            [
                new LedgerEntry(accountId, Bucket.Available, -amount),
                new LedgerEntry(accountId, Bucket.Reserved, amount),
            ]);
            return (null, posting, [account], reservation, true);
        });
}
