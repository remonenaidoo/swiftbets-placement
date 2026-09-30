using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.Contracts.Money;
using SwiftBets.Contracts.Placement;
using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Coupons;
using SwiftBets.Placement.Domain.Sagas;

namespace SwiftBets.Placement.Application.Placing;

/// <summary>
/// The placement saga. Its intent is written first, so every later step (wallet reserve, coupon persistence, capture)
/// can be completed or compensated by the orphan sweeper if this process dies between them.
/// </summary>
public sealed class PlaceCouponHandler(
    ICouponStore store,
    IOfferReader offer,
    IWalletClient wallet,
    IFaultPoint faults,
    IOptions<PlacementOptions> options,
    TimeProvider time)
{
    public const string FaultAfterReserve = "placement.after-reserve";
    public const string FaultAfterPersist = "placement.after-persist";

    public async Task<PlacementResult> HandleAsync(PlaceCouponCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var hash = RequestHash(command);
        var intent = new SagaIntent(Guid.CreateVersion7(), command.PunterId, command.IdempotencyKey, hash, command.Stake, command.Currency,
            SagaState.Started, null, null, null, now, now.AddSeconds(options.Value.SagaDeadlineSeconds));

        var existing = await store.TryStartAsync(intent);
        if (existing is not null)
        {
            return Replay(existing, hash);
        }

        var couponId = intent.CouponId;
        var quotes = await offer.QuoteAsync(command.Legs, cancellationToken);
        var (legs, rejection) = CouponQuote.Evaluate(command.Legs, quotes, command.Stake, RiskLimits.Default);
        if (rejection is not null)
        {
            return await RejectAsync(command, couponId, rejection.Code, rejection.Message, rejection.CurrentPrices);
        }

        var accepted = legs!;
        var totalOdds = PayoutMath.TotalOdds(accepted.Select(l => l.Odds));
        var payout = PayoutMath.Payout(command.Stake, totalOdds);
        var liability = await store.FixtureLiabilityAsync([.. accepted.Select(l => l.FixtureId)], cancellationToken);
        if (liability.Any(l => l.Value + payout > RiskLimits.Default.MaxFixtureLiability))
        {
            return await RejectAsync(command, couponId, "fixture_liability_exceeded", "The fixture has reached its liability limit.");
        }

        var reserve = await wallet.ReserveAsync(SagaKeys.Reserve(couponId), command.PunterId, command.Stake, command.Currency, $"coupon {couponId}");
        if (reserve.Status == WalletCallStatus.Unavailable)
        {
            return new PlacementResult(PlacementResponses.Unavailable,
                PlacementResponses.Error(couponId, PlacementResponses.Unavailable, "wallet_unavailable", "The wallet is unavailable; the coupon was not placed and no funds are held."), false);
        }

        if (reserve.Status == WalletCallStatus.Refused)
        {
            return await RejectAsync(command, couponId, reserve.FailureCode ?? "wallet_refused", "The wallet refused the stake.");
        }

        var reservationId = reserve.ReservationId!.Value;
        if (!await store.TryMarkReservedAsync(couponId, reservationId))
        {
            return await AbandonAsync(couponId, reservationId);
        }

        await faults.HitAsync(FaultAfterReserve, CancellationToken.None);

        var coupon = new PlacedCoupon(couponId, command.PunterId, accepted.Count == 1 ? BetType.Single : BetType.Accumulator,
            command.Stake, command.Currency, totalOdds, payout, accepted, time.GetUtcNow());
        var body = PlacementResponses.Coupon(coupon);
        if (!await store.TryPersistAsync(coupon, PlacementResponses.Placed, body, ToEvent(coupon)))
        {
            return await AbandonAsync(couponId, reservationId);
        }

        await faults.HitAsync(FaultAfterPersist, CancellationToken.None);

        if ((await wallet.CaptureAsync(SagaKeys.Capture(couponId), reservationId)).Status == WalletCallStatus.Succeeded)
        {
            await store.MarkCompletedAsync(couponId);
        }

        return new PlacementResult(PlacementResponses.Placed, body, false);
    }

    /// <summary>The sweeper claimed this saga while it was in flight; give the hold back rather than leave it to chance.</summary>
    private async Task<PlacementResult> AbandonAsync(Guid couponId, Guid reservationId)
    {
        await wallet.ReleaseAsync(SagaKeys.Release(couponId), reservationId);
        return new PlacementResult(PlacementResponses.Unavailable,
            PlacementResponses.Error(couponId, PlacementResponses.Unavailable, "coupon_not_placed", "Placement took too long and was rolled back; no funds are held."), false);
    }

    private async Task<PlacementResult> RejectAsync(PlaceCouponCommand command, Guid couponId, string code, string message, IReadOnlyList<QuotedPrice>? prices = null)
    {
        var status = code == "price_changed" ? PlacementResponses.Conflict : PlacementResponses.Refused;
        var body = PlacementResponses.Error(couponId, status, code, message, prices);
        await store.MarkRejectedAsync(couponId, status, body, new CouponRejectedV1(couponId, command.PunterId, code, time.GetUtcNow()));
        return new PlacementResult(status, body, false);
    }

    private static PlacementResult Replay(SagaIntent existing, string hash)
    {
        if (!string.Equals(existing.RequestHash, hash, StringComparison.Ordinal))
        {
            return new PlacementResult(PlacementResponses.Refused,
                PlacementResponses.Error(existing.CouponId, PlacementResponses.Refused, "idempotency_conflict", "This Idempotency-Key was used for a different coupon."), false);
        }

        return existing.ResponseJson is { } body
            ? new PlacementResult(existing.ResponseStatus!.Value, body, true)
            : new PlacementResult(PlacementResponses.Conflict,
                PlacementResponses.Error(existing.CouponId, PlacementResponses.Conflict, "placement_in_progress", "This coupon is still being placed; retry shortly."), true);
    }

    private static CouponPlacedV1 ToEvent(PlacedCoupon coupon) => new(
        coupon.CouponId,
        coupon.PunterId,
        coupon.BetType,
        new Money(coupon.Stake, coupon.Currency),
        coupon.TotalOdds,
        new Money(coupon.PotentialPayout, coupon.Currency),
        [.. coupon.Legs.Select(l => new CouponLegV1(l.LegId, l.FixtureId, l.MarketId, l.SelectionId, l.Odds, l.OfferVersion))],
        coupon.PlacedAt);

    private static string RequestHash(PlaceCouponCommand command)
    {
        var canonical = JsonSerializer.Serialize(new { command.Stake, command.Currency, command.Legs });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
