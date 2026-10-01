SELECT DISTINCT Permission FROM accounts.RolePermissions WHERE Role IN @Roles ORDER BY Permission;
