UPDATE placement.SagaIntents SET State = 2, ReservationId = @ReservationId, UpdatedAt = @Now
WHERE CouponId = @CouponId AND State = 1;
