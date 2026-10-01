SELECT TOP (@Limit) CAST(3 AS tinyint) AS Kind, a.AccountId, CAST(NULL AS uniqueidentifier) AS PostingId,
       COALESCE(h.Held, 0) AS Expected, a.Reserved AS Actual
FROM wallet.Accounts a
LEFT JOIN (SELECT AccountId, SUM(Amount) AS Held FROM wallet.Reservations WHERE State = 1 GROUP BY AccountId) h ON h.AccountId = a.AccountId
WHERE a.Kind = 1 AND a.Reserved <> COALESCE(h.Held, 0)
ORDER BY a.AccountId;
