-- Rolls back 0003_bets_and_bankers. Run only while system bets are off: a coupon with bankers or several bets loses its shape.
DROP TABLE IF EXISTS placement.CouponBets;
ALTER TABLE placement.CouponLegs DROP CONSTRAINT IF EXISTS DF_CouponLegs_IsBanker;
ALTER TABLE placement.CouponLegs DROP COLUMN IF EXISTS IsBanker;
DELETE FROM dbo.SchemaVersions WHERE ScriptName = 'SwiftBets.Placement.Migrator.Migrations.0003_bets_and_bankers.sql';
