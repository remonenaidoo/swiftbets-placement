using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Sagas;

namespace SwiftBets.Placement.Infrastructure.Persistence;

internal sealed record SagaRow(
    Guid CouponId, Guid PunterId, string IdempotencyKey, string RequestHash, long Stake, string Currency, byte State,
    Guid? ReservationId, int? ResponseStatus, string? ResponseJson, DateTimeOffset CreatedAt, DateTimeOffset DeadlineAt)
{
    public SagaIntent ToIntent() => new(CouponId, PunterId, IdempotencyKey, RequestHash, Stake, Currency, (SagaState)State, ReservationId, ResponseStatus, ResponseJson, CreatedAt, DeadlineAt);
}
