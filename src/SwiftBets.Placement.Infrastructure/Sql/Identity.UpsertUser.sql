MERGE auth.Users WITH (HOLDLOCK) AS target
USING (SELECT @UserId AS UserId) AS source ON target.UserId = source.UserId
WHEN MATCHED THEN UPDATE SET Username = @Username, PasswordHash = @PasswordHash, Roles = @Roles
WHEN NOT MATCHED THEN INSERT (UserId, Username, PasswordHash, Roles, CreatedAt) VALUES (@UserId, @Username, @PasswordHash, @Roles, SYSUTCDATETIME());
