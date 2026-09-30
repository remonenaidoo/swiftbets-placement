SELECT AccountId, Kind, Currency, Available, Reserved, IsBlacklisted
FROM wallet.Accounts
WHERE AccountId = @AccountId;
