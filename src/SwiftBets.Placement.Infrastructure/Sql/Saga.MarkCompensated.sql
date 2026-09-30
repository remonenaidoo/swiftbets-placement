UPDATE placement.SagaIntents SET State = 5, ResponseStatus = @ResponseStatus, ResponseJson = @ResponseJson, LeaseUntil = NULL, UpdatedAt = @Now
WHERE CouponId = @CouponId AND State = 7;
