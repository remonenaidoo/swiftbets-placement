-- Punter balances against their entries. House and funding balances are not projected (a hot row on every bet), so
-- they are covered by the posting and ledger-total checks instead.
WITH sums AS (
    SELECT e.AccountId,
           SUM(CASE WHEN e.Bucket = 1 THEN e.Amount ELSE 0 END) AS AvailableSum,
           SUM(CASE WHEN e.Bucket = 2 THEN e.Amount ELSE 0 END) AS ReservedSum
    FROM wallet.LedgerEntries e
    JOIN wallet.Accounts a ON a.AccountId = e.AccountId AND a.Kind = 1
    GROUP BY e.AccountId)
SELECT TOP (@Limit) v.Kind, a.AccountId, CAST(NULL AS uniqueidentifier) AS PostingId, v.Expected, v.Actual
FROM wallet.Accounts a
LEFT JOIN sums s ON s.AccountId = a.AccountId
CROSS APPLY (VALUES (CAST(1 AS tinyint), COALESCE(s.AvailableSum, 0), a.Available),
                    (CAST(2 AS tinyint), COALESCE(s.ReservedSum, 0), a.Reserved)) v (Kind, Expected, Actual)
WHERE a.Kind = 1 AND v.Expected <> v.Actual
ORDER BY a.AccountId, v.Kind;
