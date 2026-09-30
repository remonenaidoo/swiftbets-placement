WITH due AS
(
    SELECT TOP (@BatchSize) *
    FROM placement.SagaIntents WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE State IN (1, 2, 3, 7)
      AND DeadlineAt < @Now
      AND (LeaseUntil IS NULL OR LeaseUntil < @Now)
    ORDER BY DeadlineAt
)
UPDATE due
SET State = CASE WHEN State IN (1, 2) THEN 7 ELSE State END,
    LeaseUntil = @LeaseUntil,
    SweepAttempts = SweepAttempts + 1,
    UpdatedAt = @Now
OUTPUT inserted.CouponId, inserted.PunterId, inserted.IdempotencyKey, inserted.RequestHash, inserted.Stake, inserted.Currency,
       inserted.State, inserted.ReservationId, inserted.ResponseStatus, inserted.ResponseJson, inserted.CreatedAt, inserted.DeadlineAt;
