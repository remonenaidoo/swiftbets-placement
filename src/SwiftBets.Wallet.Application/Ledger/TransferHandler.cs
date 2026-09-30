using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ledger;

/// <summary>Credits (winnings, refunds, top-ups) and debits (resettlement clawbacks) between a punter and the house or funding account.</summary>
public sealed class TransferHandler(PostingRunner runner)
{
    public Task<WalletOutcome> CreditAsync(string idempotencyKey, Guid accountId, long amount, string currency, string reference) =>
        MoveAsync(PostingKind.Credit, WellKnownAccounts.House, idempotencyKey, accountId, amount, currency, reference);

    public Task<WalletOutcome> TopUpAsync(string idempotencyKey, Guid accountId, long amount, string currency, string reference) =>
        MoveAsync(PostingKind.TopUp, WellKnownAccounts.Funding, idempotencyKey, accountId, amount, currency, reference);

    public Task<WalletOutcome> DebitAsync(string idempotencyKey, Guid accountId, long amount, string currency, string reference) =>
        runner.RunAsync(idempotencyKey, PostingKind.Debit, accountId, amount, async transaction =>
        {
            var account = await transaction.LockAccountAsync(accountId);
            if (account is null)
            {
                return (WalletFailure.AccountNotFound, null, [], null, false);
            }

            return account.Pay(amount, currency) is { } failure
                ? (failure, null, [], null, false)
                : (null, new Posting(Guid.CreateVersion7(), PostingKind.Debit, idempotencyKey, reference,
                    [new(accountId, Bucket.Available, -amount), new(WellKnownAccounts.House, Bucket.Available, amount)]), [account], null, false);
        });

    private Task<WalletOutcome> MoveAsync(PostingKind kind, Guid source, string idempotencyKey, Guid accountId, long amount, string currency, string reference) =>
        runner.RunAsync(idempotencyKey, kind, accountId, amount, async transaction =>
        {
            var account = await transaction.LockAccountAsync(accountId);
            if (account is null)
            {
                return (WalletFailure.AccountNotFound, null, [], null, false);
            }

            return account.Receive(amount, currency) is { } failure
                ? (failure, null, [], null, false)
                : (null, new Posting(Guid.CreateVersion7(), kind, idempotencyKey, reference,
                    [new(source, Bucket.Available, -amount), new(accountId, Bucket.Available, amount)]), [account], null, false);
        });
}
