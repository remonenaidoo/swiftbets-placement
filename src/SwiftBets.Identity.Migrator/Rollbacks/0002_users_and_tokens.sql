-- Rolls back 0002_users_and_tokens. DESTROYS every account and token: only for a database with no customers yet,
-- or after restoring from a backup taken before the migration.
DROP TABLE IF EXISTS accounts.OneTimeTokens;
DROP TABLE IF EXISTS accounts.RefreshTokens;
DROP TABLE IF EXISTS accounts.StatusHistory;
DROP TABLE IF EXISTS accounts.RolePermissions;
DROP TABLE IF EXISTS accounts.UserRoles;
DROP TABLE IF EXISTS accounts.Users;
DELETE FROM dbo.SchemaVersions WHERE ScriptName = 'SwiftBets.Identity.Migrator.Migrations.0002_users_and_tokens.sql';
