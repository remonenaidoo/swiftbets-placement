SELECT CouponId, PunterId, BetType, Stake, Currency, TotalOdds, PotentialPayout, PlacedAt FROM placement.Coupons WHERE CouponId = @CouponId;
SELECT LegId, FixtureId, MarketId, SelectionId, Odds, OfferVersion FROM placement.CouponLegs WHERE CouponId = @CouponId;
