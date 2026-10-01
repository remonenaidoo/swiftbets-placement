IF EXISTS (SELECT 1 FROM accounts.Users WHERE UserId = @UserId) AND NOT EXISTS (SELECT 1 FROM accounts.RefreshTokens WHERE TokenHash = @TokenHash)
    INSERT INTO accounts.RefreshTokens (TokenHash, UserId, FamilyId, ExpiresAt, ConsumedAt, RevokedAt)
    VALUES (@TokenHash, @UserId, @FamilyId, @ExpiresAt, @ConsumedAt, @RevokedAt);
