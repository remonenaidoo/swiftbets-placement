using SwiftBets.Contracts.Placement;

namespace SwiftBets.Placement.Application.Ports;

public interface ICouponStore
{
    /// <summary>Inserts the intent, or returns the one that already owns this punter's idempotency key.</summary>
    Task<SagaIntent?> TryStartAsync(SagaIntent intent);

    /// <summary>Started to Reserved; false when the sweeper has already claimed the saga.</summary>
    Task<bool> TryMarkReservedAsync(Guid couponId, Guid reservationId);

    /// <summary>Terminal refusal; the rejection event is written to the outbox in the same transaction.</summary>
    Task MarkRejectedAsync(Guid couponId, int responseStatus, string responseJson, CouponRejectedV1 rejected);

    /// <summary>
    /// Coupon, legs, bets, the placed events (outbox) and the Persisted state commit together or not at all. V1 is null
    /// for a coupon only V2 can describe.
    /// </summary>
    Task<bool> TryPersistAsync(PlacedCoupon coupon, int responseStatus, string responseJson, CouponPlacedV1? placed, CouponPlacedV2 placedV2);

    Task MarkCompletedAsync(Guid couponId);

    Task MarkCompensatedAsync(Guid couponId, int responseStatus, string responseJson);

    /// <summary>
    /// Claims non-terminal intents past their deadline under a lease, so concurrent sweepers never share one. Unpersisted
    /// intents move to Compensating in the same statement, which stops the live saga advancing them.
    /// </summary>
    Task<IReadOnlyList<SagaIntent>> ClaimExpiredAsync(DateTimeOffset now, int batchSize, TimeSpan lease, CancellationToken cancellationToken);

    Task<PlacedCoupon?> GetCouponAsync(Guid couponId, CancellationToken cancellationToken);
}
