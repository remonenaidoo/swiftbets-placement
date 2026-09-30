INSERT INTO placement.SagaIntents (CouponId, PunterId, IdempotencyKey, RequestHash, Stake, Currency, State, CreatedAt, UpdatedAt, DeadlineAt)
VALUES (@CouponId, @PunterId, @IdempotencyKey, @RequestHash, @Stake, @Currency, 1, @CreatedAt, @CreatedAt, @DeadlineAt);
