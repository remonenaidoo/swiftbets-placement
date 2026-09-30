SELECT AccountId, Kind, Currency, Available, Reserved, IsBlacklisted
FROM wallet.Accounts WITH (UPDLOCK, ROWLOCK)
WHERE AccountId = @AccountId;
