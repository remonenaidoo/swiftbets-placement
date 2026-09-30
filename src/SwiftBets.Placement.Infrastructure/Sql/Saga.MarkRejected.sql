UPDATE placement.SagaIntents SET State = 6, ResponseStatus = @ResponseStatus, ResponseJson = @ResponseJson, UpdatedAt = @Now
WHERE CouponId = @CouponId AND State = 1;
