UPDATE accounts.Users SET PasswordHash = @PasswordHash, UpdatedAt = @Now WHERE UserId = @UserId;
