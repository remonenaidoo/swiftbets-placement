using SwiftBets.Wallet.Application.Ports;
using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ledger;

/// <summary>Captures a held reservation into the house, or releases it back to the punter.</summary>
public sealed class SettleReservationHandler(PostingRunner runner, IWalletStore store)
{
    public Task<WalletOutcome> CaptureAsync(string idempotencyKey, Guid reservationId, CancellationToken cancellationToken) =>
        RunAsync(idempotencyKey, reservationId, capture: true, cancellationToken);

    public Task<WalletOutcome> ReleaseAsync(string idempotencyKey, Guid reservationId, CancellationToken cancellationToken) =>
        RunAsync(idempotencyKey, reservationId, capture: false, cancellationToken);

    private async Task<WalletOutcome> RunAsync(string idempotencyKey, Guid reservationId, bool capture, CancellationToken cancellationToken)
    {
        var known = await store.GetReservationAsync(reservationId, cancellationToken);
        if (known is null)
        {
            return WalletOutcome.Failed(WalletFailure.ReservationNotFound);
        }

        var kind = capture ? PostingKind.Capture : PostingKind.Release;
        return await runner.RunAsync(idempotencyKey, kind, known.AccountId, known.Amount, async transaction =>
        {
            var reservation = (await transaction.LockReservationAsync(reservationId))!;
            var account = (await transaction.LockAccountAsync(reservation.AccountId))!;
            var failure = reservation.MoveTo(capture ? ReservationState.Captured : ReservationState.Released)
                ?? (capture ? account.ConsumeReservation(reservation.Amount) : account.ReturnReservation(reservation.Amount));
            if (failure is not null)
            {
                return (failure, null, [], null, false);
            }

            IReadOnlyList<LedgerEntry> entries = capture
                ? [new(account.AccountId, Bucket.Reserved, -reservation.Amount), new(WellKnownAccounts.House, Bucket.Available, reservation.Amount)]
                : [new(account.AccountId, Bucket.Reserved, -reservation.Amount), new(account.AccountId, Bucket.Available, reservation.Amount)];
            return (null, new Posting(Guid.CreateVersion7(), kind, idempotencyKey, reservation.Reference, entries), [account], reservation, false);
        });
    }
}
