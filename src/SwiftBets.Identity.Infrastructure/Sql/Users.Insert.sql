INSERT INTO accounts.Users (UserId, Email, NormalizedEmail, Username, PasswordHash, DateOfBirth, Brand, Country, Currency, Status, ParentUserId, CreatedAt, UpdatedAt)
VALUES (@UserId, @Email, @NormalizedEmail, @Username, @PasswordHash, @DateOfBirth, @Brand, @Country, @Currency, @Status, @ParentUserId, @Now, @Now);
