MERGE accounts.Users WITH (HOLDLOCK) AS target
USING (SELECT @UserId AS UserId) AS source ON target.UserId = source.UserId
WHEN MATCHED THEN UPDATE SET PasswordHash = @PasswordHash, UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT (UserId, Email, NormalizedEmail, Username, PasswordHash, DateOfBirth, Brand, Country, Currency, Status, EmailVerifiedAt, CreatedAt, UpdatedAt)
    VALUES (@UserId, @Email, @NormalizedEmail, @Username, @PasswordHash, @DateOfBirth, @Brand, @Country, @Currency, @Status, @Now, @Now, @Now);
DELETE FROM accounts.UserRoles WHERE UserId = @UserId;
