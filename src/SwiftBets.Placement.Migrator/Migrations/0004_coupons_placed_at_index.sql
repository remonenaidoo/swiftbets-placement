CREATE INDEX IX_Coupons_PlacedAt ON placement.Coupons (PlacedAt) INCLUDE (Stake, PotentialPayout, Currency);
