SELECT CouponId, PunterId, BetType, Stake, Currency, TotalOdds, PotentialPayout, PlacedAt FROM placement.Coupons WHERE CouponId = @CouponId;
SELECT LegId, FixtureId, MarketId, SelectionId, Odds, OfferVersion, IsBanker FROM placement.CouponLegs WHERE CouponId = @CouponId;
SELECT BetId, Name, Folds, Lines, UnitStake, Stake, PotentialPayout FROM placement.CouponBets WHERE CouponId = @CouponId ORDER BY BetId;
