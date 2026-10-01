SELECT TOP (1) RunId, StartedAt, CompletedAt, AccountsChecked
FROM wallet.ReconciliationRuns
ORDER BY Sequence DESC;
