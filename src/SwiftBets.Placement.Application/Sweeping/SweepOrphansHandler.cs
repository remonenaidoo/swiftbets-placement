using Microsoft.Extensions.Logging;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Sagas;

namespace SwiftBets.Placement.Application.Sweeping;

/// <summary>
/// Resolves sagas that never finished (a crash, a timeout, a wallet outage). A persisted coupon is a committed bet, so
/// its capture is completed. Anything not persisted is compensated: whatever the wallet holds under the saga's reserve
/// key is released. The wallet's own idempotency makes a duplicate sweep harmless.
/// </summary>
public sealed partial class SweepOrphansHandler(ICouponStore store, IWalletClient wallet, TimeProvider time, ILogger<SweepOrphansHandler> logger)
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var resolved = 0;
        foreach (var intent in await store.ClaimExpiredAsync(time.GetUtcNow(), 50, Lease, cancellationToken))
        {
            if (await ResolveAsync(intent, cancellationToken))
            {
                resolved++;
            }
        }

        return resolved;
    }

    private async Task<bool> ResolveAsync(SagaIntent intent, CancellationToken cancellationToken)
    {
        if (intent.State == SagaState.Persisted)
        {
            var capture = await wallet.CaptureAsync(SagaKeys.Capture(intent.CouponId), intent.ReservationId!.Value);
            if (capture.Status != WalletCallStatus.Succeeded)
            {
                return false;
            }

            await store.MarkCompletedAsync(intent.CouponId);
            LogCompleted(intent.CouponId);
            return true;
        }

        var reservation = await wallet.FindReservationAsync(SagaKeys.Reserve(intent.CouponId), cancellationToken);
        if (reservation.Status == WalletCallStatus.Unavailable)
        {
            return false;
        }

        if (reservation is { Status: WalletCallStatus.Succeeded, ReservationState: "Held" })
        {
            if ((await wallet.ReleaseAsync(SagaKeys.Release(intent.CouponId), reservation.ReservationId!.Value)).Status != WalletCallStatus.Succeeded)
            {
                return false;
            }
        }
        else if (reservation is { Status: WalletCallStatus.Succeeded, ReservationState: "Captured" })
        {
            LogCapturedWithoutCoupon(intent.CouponId);
            return false;
        }

        await store.MarkCompensatedAsync(intent.CouponId, PlacementResponses.Unavailable,
            PlacementResponses.Error(intent.CouponId, PlacementResponses.Unavailable, "coupon_not_placed", "Placement did not complete; any held funds were released."));
        LogCompensated(intent.CouponId, intent.State);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sweeper completed capture for coupon {CouponId}")]
    private partial void LogCompleted(Guid couponId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sweeper compensated coupon {CouponId} from state {State}")]
    private partial void LogCompensated(Guid couponId, SagaState state);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Coupon {CouponId} has a captured reservation but no persisted coupon; needs manual reconciliation")]
    private partial void LogCapturedWithoutCoupon(Guid couponId);
}
