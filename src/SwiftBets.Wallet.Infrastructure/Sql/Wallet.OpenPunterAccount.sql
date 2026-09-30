INSERT INTO wallet.Accounts (AccountId, Kind, Currency, CreatedAt)
SELECT @AccountId, 1, @Currency, @Now
WHERE NOT EXISTS (SELECT 1 FROM wallet.Accounts WITH (UPDLOCK, HOLDLOCK) WHERE AccountId = @AccountId);
