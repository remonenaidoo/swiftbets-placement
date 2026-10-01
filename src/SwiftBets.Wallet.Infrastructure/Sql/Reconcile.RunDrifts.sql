SELECT Kind, AccountId, PostingId, Expected, Actual
FROM wallet.ReconciliationDrifts
WHERE RunId = @RunId
ORDER BY DriftId;
