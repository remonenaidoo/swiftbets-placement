-- Rolls back 0004_reconciliation. Loses only reconciliation history, which the next run rebuilds.
DROP TABLE IF EXISTS wallet.ReconciliationDrifts;
DROP TABLE IF EXISTS wallet.ReconciliationRuns;
DELETE FROM dbo.SchemaVersions WHERE ScriptName = 'SwiftBets.Wallet.Migrator.Migrations.0004_reconciliation.sql';
