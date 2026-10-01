UPDATE accounts.OneTimeTokens SET UsedAt = @Now
OUTPUT inserted.UserId
WHERE TokenHash = @TokenHash AND Purpose = @Purpose AND UsedAt IS NULL AND ExpiresAt > @Now;
