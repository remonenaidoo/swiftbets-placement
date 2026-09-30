SELECT l.FixtureId, SUM(c.PotentialPayout) AS Liability
FROM placement.CouponLegs l
JOIN placement.Coupons c ON c.CouponId = l.CouponId
WHERE l.FixtureId IN @FixtureIds AND c.IsOpen = 1
GROUP BY l.FixtureId;
