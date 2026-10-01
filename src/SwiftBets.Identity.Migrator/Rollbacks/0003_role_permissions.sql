-- Rolls back 0003_role_permissions. Staff tokens issued afterwards carry no permissions until it is reapplied.
DELETE FROM accounts.RolePermissions;
DELETE FROM dbo.SchemaVersions WHERE ScriptName = 'SwiftBets.Identity.Migrator.Migrations.0003_role_permissions.sql';
