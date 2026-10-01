INSERT INTO wallet.ReconciliationRuns (RunId, StartedAt, CompletedAt, AccountsChecked, DriftCount)
VALUES (@RunId, @StartedAt, @CompletedAt, @AccountsChecked, @DriftCount);
