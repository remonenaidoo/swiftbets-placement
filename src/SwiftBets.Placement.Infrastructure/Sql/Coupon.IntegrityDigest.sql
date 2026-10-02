SELECT TOP (@Limit) CouponId, Stake, PotentialPayout, Currency, PlacedAt
FROM placement.Coupons
WHERE PlacedAt >= @From AND PlacedAt < @To
ORDER BY PlacedAt;
