using SwiftBets.Placement.Domain.Sagas;

namespace SwiftBets.Placement.Application.Ports;

public sealed record SagaIntent(
    Guid CouponId,
    Guid PunterId,
    string IdempotencyKey,
    string RequestHash,
    long Stake,
    string Currency,
    SagaState State,
    Guid? ReservationId,
    int? ResponseStatus,
    string? ResponseJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset DeadlineAt);
