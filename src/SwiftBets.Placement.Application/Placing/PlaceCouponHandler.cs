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
    ILiabilityLedger liabilities,
    IOfferReader offer,
    IWalletClient wallet,
    IPlacementSettings settings,
    IFaultPoint faults,
    IOptions<PlacementOptions> options,
    TimeProvider time)
{
    public const string FaultAfterReserve = "placement.after-reserve";
    public const string FaultAfterPersist = "placement.after-persist";

    /// <summary>Config flag that opens system bets and bankers, once settlement reads V2 coupons.</summary>
    public const string SystemBetsFlag = "system-bets";

    /// <summary>Every fixture on offer is football today; the live delay is keyed by sport so others slot in.</summary>
    public const string Sport = "soccer";

    public async Task<PlacementResult> HandleAsync(PlaceCouponCommand command, CancellationToken cancellationToken)
    {
        // An in-play coupon waits out the live delay before its saga starts, so the wait never counts against the saga
        // deadline; the quote below then re-reads the offer, which is the refresh that catches a suspension or a move.
        if (!settings.IsStopped && settings.LiveDelaySeconds(Sport) is > 0 and var delay
            && (await offer.QuoteAsync(command.Legs, cancellationToken)).Values.Any(p => p.IsLive))
        {
            await Task.Delay(TimeSpan.FromSeconds(delay), time, cancellationToken);
        }

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
        if (settings.IsStopped)
        {
            return await RejectAsync(command, couponId, "placement_suspended", "Betting is paused right now. Try again shortly.");
        }

        if (settings.MaxStake(command.Currency) is { } maxStake && command.Stake > maxStake)
        {
            return await RejectAsync(command, couponId, "stake_above_limit", $"The most you can stake on one coupon is {Amount(maxStake, command.Currency)}.");
        }

        var quotes = await offer.QuoteAsync(command.Legs, cancellationToken);
        var (legs, rejection) = CouponQuote.Evaluate(command.Legs, quotes, command.Stake, RiskLimits.Default);
        if (rejection is not null)
        {
            return await RejectAsync(command, couponId, rejection.Code, rejection.Message, rejection.CurrentPrices);
        }

        var accepted = legs!;
        if (settings.IsPreMatchOnly && accepted.Any(l => quotes[CouponQuote.Key(l.FixtureId, l.MarketId, l.SelectionId)].IsLive))
        {
            return await RejectAsync(command, couponId, "live_betting_unavailable", "Betting on matches in play is closed right now.");
        }

        if (command.Bets is null && accepted.Any(l => l.IsBanker))
        {
            return await RejectAsync(command, couponId, "invalid_bets", "Bankers need a system bet such as a Trixie.");
        }

        var (bets, betRejection) = BetPricing.Price(accepted, command.Bets ?? [BetPricing.Accumulator(accepted.Count, command.Stake)]);
        if (betRejection is not null)
        {
            return await RejectAsync(command, couponId, betRejection.Code, betRejection.Message);
        }

        var priced = bets!;
        var isSystem = IsSystem(accepted, priced);
        if (isSystem && !settings.IsEnabled(SystemBetsFlag))
        {
            return await RejectAsync(command, couponId, "system_bets_unavailable", "System bets and bankers are not available yet.");
        }

        var totalStake = priced.Sum(b => b.Stake);
        if (totalStake != command.Stake)
        {
            return await RejectAsync(command, couponId, "stake_mismatch", $"The bets total {Amount(totalStake, command.Currency)}, not {Amount(command.Stake, command.Currency)}.");
        }

        var payout = priced.Sum(b => b.PotentialPayout);
        if (CouponQuote.CheckPayout(payout, RiskLimits.Default) is { } tooHigh)
        {
            return await RejectAsync(command, couponId, tooHigh.Code, tooHigh.Message);
        }

        if (settings.MaxPayout(command.Currency) is { } maxPayout && payout > maxPayout)
        {
            return await RejectAsync(command, couponId, "payout_above_limit", $"The most one coupon can pay is {Amount(maxPayout, command.Currency)}; lower the stake.");
        }

        var totalOdds = isSystem ? decimal.Round((decimal)payout / totalStake, 6, MidpointRounding.ToZero) : PayoutMath.TotalOdds(accepted.Select(l => l.Odds));
        var liability = await liabilities.GetAsync([.. accepted.Select(l => l.FixtureId)], cancellationToken);
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
            return await RejectAsync(command, couponId, reserve.FailureCode ?? "wallet_refused", WalletRefusalMessages.For(reserve.FailureCode, reserve.FailureDetail, command.Currency));
        }

        var reservationId = reserve.ReservationId!.Value;
        if (!await store.TryMarkReservedAsync(couponId, reservationId))
        {
            return await AbandonAsync(couponId, reservationId);
        }

        await faults.HitAsync(FaultAfterReserve, CancellationToken.None);

        var coupon = new PlacedCoupon(couponId, command.PunterId, isSystem ? BetType.System : accepted.Count == 1 ? BetType.Single : BetType.Accumulator,
            command.Stake, command.Currency, totalOdds, payout, accepted, time.GetUtcNow(), priced);
        var body = PlacementResponses.Coupon(coupon);
        if (!await store.TryPersistAsync(coupon, PlacementResponses.Placed, body, ToEventV2(coupon)))
        {
            return await AbandonAsync(couponId, reservationId);
        }

        await liabilities.AddAsync([.. accepted.Select(l => l.FixtureId)], payout);
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

    private static bool IsSystem(IReadOnlyList<AcceptedLeg> legs, IReadOnlyList<PricedBet> bets) =>
        legs.Any(l => l.IsBanker) || bets.Count != 1 || bets[0].Lines != 1 || bets[0].Folds[0] != legs.Count;

    private static CouponPlacedV2 ToEventV2(PlacedCoupon coupon) => new(
        coupon.CouponId,
        coupon.PunterId,
        new Money(coupon.Stake, coupon.Currency),
        new Money(coupon.PotentialPayout, coupon.Currency),
        [.. coupon.Legs.Select(l => new CouponLegV2(l.LegId, l.FixtureId, l.MarketId, l.SelectionId, l.Odds, l.OfferVersion, l.IsBanker))],
        [.. coupon.Bets.Select(b => new CouponBetV2(b.BetId, b.Name, b.Folds, b.Lines, new Money(b.UnitStake, coupon.Currency), new Money(b.Stake, coupon.Currency), new Money(b.PotentialPayout, coupon.Currency)))],
        coupon.PlacedAt);

    private static string Amount(long minorUnits, string currency) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{currency} {minorUnits / 100m:0.00}");

    private static string RequestHash(PlaceCouponCommand command)
    {
        // Coupons without bets or bankers keep the hash they had before V2, so a retry across a deploy still replays.
        var canonical = command.Bets is null && !command.Legs.Any(l => l.IsBanker)
            ? JsonSerializer.Serialize(new { command.Stake, command.Currency, Legs = command.Legs.Select(l => new { l.FixtureId, l.MarketId, l.SelectionId, l.RequestedOdds, l.OfferVersion }) })
            : JsonSerializer.Serialize(new { command.Stake, command.Currency, command.Legs, command.Bets });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
