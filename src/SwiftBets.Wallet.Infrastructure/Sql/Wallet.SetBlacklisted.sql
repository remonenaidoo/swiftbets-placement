UPDATE wallet.Accounts SET IsBlacklisted = @IsBlacklisted WHERE AccountId = @AccountId AND Kind = 1;
SELECT @@ROWCOUNT;
