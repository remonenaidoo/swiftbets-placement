using SwiftBets.Contracts.Placement;
using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Sagas;

namespace SwiftBets.Placement.Application.Tests;

internal sealed class InMemoryCouponStore : ICouponStore
{
    public Dictionary<Guid, SagaIntent> Intents { get; } = [];

    public List<PlacedCoupon> Coupons { get; } = [];

    public List<CouponPlacedV1?> PlacedV1 { get; } = [];

    public List<CouponPlacedV2> PlacedV2 { get; } = [];

    public Task<SagaIntent?> TryStartAsync(SagaIntent intent)
    {
        var existing = Intents.Values.FirstOrDefault(i => i.PunterId == intent.PunterId && i.IdempotencyKey == intent.IdempotencyKey);
        if (existing is null)
        {
            Intents[intent.CouponId] = intent;
        }

        return Task.FromResult(existing);
    }

    public Task<bool> TryMarkReservedAsync(Guid couponId, Guid reservationId) => Transition(couponId, SagaState.Started, i => i with { State = SagaState.Reserved, ReservationId = reservationId });

    public Task MarkRejectedAsync(Guid couponId, int responseStatus, string responseJson, CouponRejectedV1 rejected) =>
        Transition(couponId, SagaState.Started, i => i with { State = SagaState.Rejected, ResponseStatus = responseStatus, ResponseJson = responseJson });

    public async Task<bool> TryPersistAsync(PlacedCoupon coupon, int responseStatus, string responseJson, CouponPlacedV1? placed, CouponPlacedV2 placedV2)
    {
        var ok = await Transition(coupon.CouponId, SagaState.Reserved, i => i with { State = SagaState.Persisted, ResponseStatus = responseStatus, ResponseJson = responseJson });
        if (ok)
        {
            Coupons.Add(coupon);
            PlacedV1.Add(placed);
            PlacedV2.Add(placedV2);
        }

        return ok;
    }

    public Task MarkCompletedAsync(Guid couponId) => Transition(couponId, SagaState.Persisted, i => i with { State = SagaState.Completed });

    public Task MarkCompensatedAsync(Guid couponId, int responseStatus, string responseJson) =>
        Transition(couponId, SagaState.Compensating, i => i with { State = SagaState.Compensated, ResponseStatus = responseStatus, ResponseJson = responseJson });

    public Task<IReadOnlyList<SagaIntent>> ClaimExpiredAsync(DateTimeOffset now, int batchSize, TimeSpan lease, CancellationToken cancellationToken)
    {
        var due = Intents.Values.Where(i => i.State is SagaState.Started or SagaState.Reserved or SagaState.Persisted or SagaState.Compensating && i.DeadlineAt < now).ToList();
        foreach (var intent in due.Where(i => i.State is SagaState.Started or SagaState.Reserved))
        {
            Intents[intent.CouponId] = intent with { State = SagaState.Compensating };
        }

        return Task.FromResult<IReadOnlyList<SagaIntent>>([.. due.Select(i => Intents[i.CouponId])]);
    }

    public Task<IReadOnlyList<CouponDigest>> DigestAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CouponDigest>>([.. Coupons.Where(c => c.PlacedAt >= from && c.PlacedAt < to).OrderBy(c => c.PlacedAt).Take(limit)
            .Select(c => new CouponDigest(c.CouponId, c.Stake, c.PotentialPayout, c.Currency, c.PlacedAt))]);

    public Task<PlacedCoupon?> GetCouponAsync(Guid couponId, CancellationToken cancellationToken) =>
        Task.FromResult(Coupons.FirstOrDefault(c => c.CouponId == couponId));

    private Task<bool> Transition(Guid couponId, SagaState expected, Func<SagaIntent, SagaIntent> change)
    {
        if (Intents[couponId].State != expected)
        {
            return Task.FromResult(false);
        }

        Intents[couponId] = change(Intents[couponId]);
        return Task.FromResult(true);
    }
}
