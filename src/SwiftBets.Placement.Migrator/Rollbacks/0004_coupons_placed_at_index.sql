-- Rolls back 0004_coupons_placed_at_index. The integrity digest still works, by scanning.
DROP INDEX IF EXISTS IX_Coupons_PlacedAt ON placement.Coupons;
DELETE FROM dbo.SchemaVersions WHERE ScriptName = 'SwiftBets.Placement.Migrator.Migrations.0004_coupons_placed_at_index.sql';
