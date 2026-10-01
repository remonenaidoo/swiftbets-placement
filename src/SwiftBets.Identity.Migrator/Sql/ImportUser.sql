IF NOT EXISTS (SELECT 1 FROM accounts.Users WHERE UserId = @UserId)
BEGIN
    INSERT INTO accounts.Users (UserId, Username, PasswordHash, Brand, Country, Currency, CreatedAt, UpdatedAt)
    VALUES (@UserId, @Username, @PasswordHash, @Brand, 'ZA', 'ZAR', @CreatedAt, SYSUTCDATETIME());
    INSERT INTO accounts.UserRoles (UserId, Role) SELECT @UserId, value FROM STRING_SPLIT(@Roles, ',') WHERE value <> '';
END
