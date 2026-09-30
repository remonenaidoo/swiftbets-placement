SELECT CouponId, PunterId, IdempotencyKey, RequestHash, Stake, Currency, State, ReservationId, ResponseStatus, ResponseJson, CreatedAt, DeadlineAt
FROM placement.SagaIntents
WHERE PunterId = @PunterId AND IdempotencyKey = @IdempotencyKey;
