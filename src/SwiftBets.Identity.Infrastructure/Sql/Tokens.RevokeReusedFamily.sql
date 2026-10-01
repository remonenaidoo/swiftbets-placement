DECLARE @family uniqueidentifier = (SELECT FamilyId FROM accounts.RefreshTokens WHERE TokenHash = @TokenHash AND ConsumedAt IS NOT NULL);
IF @family IS NOT NULL
    UPDATE accounts.RefreshTokens SET RevokedAt = @Now WHERE FamilyId = @family AND RevokedAt IS NULL;
SELECT CASE WHEN @family IS NULL THEN 0 ELSE 1 END;
