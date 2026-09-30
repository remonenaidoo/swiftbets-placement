UPDATE placement.SagaIntents SET State = 4, LeaseUntil = NULL, UpdatedAt = @Now
WHERE CouponId = @CouponId AND State = 3;
