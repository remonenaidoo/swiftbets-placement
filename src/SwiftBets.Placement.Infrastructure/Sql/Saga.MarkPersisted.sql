UPDATE placement.SagaIntents SET State = 3, ResponseStatus = @ResponseStatus, ResponseJson = @ResponseJson, UpdatedAt = @Now
WHERE CouponId = @CouponId AND State = 2;
