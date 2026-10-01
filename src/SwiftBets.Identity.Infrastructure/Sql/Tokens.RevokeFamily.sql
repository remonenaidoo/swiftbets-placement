UPDATE accounts.RefreshTokens SET RevokedAt = @Now
WHERE RevokedAt IS NULL AND FamilyId = (SELECT FamilyId FROM accounts.RefreshTokens WHERE TokenHash = @TokenHash);
