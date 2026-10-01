UPDATE accounts.RefreshTokens SET RevokedAt = @Now WHERE UserId = @UserId AND RevokedAt IS NULL;
