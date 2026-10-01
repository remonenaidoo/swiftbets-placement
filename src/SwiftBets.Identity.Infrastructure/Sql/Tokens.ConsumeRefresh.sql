UPDATE accounts.RefreshTokens SET ConsumedAt = @Now
OUTPUT inserted.UserId, inserted.FamilyId
WHERE TokenHash = @TokenHash AND ConsumedAt IS NULL AND RevokedAt IS NULL AND ExpiresAt > @Now;
