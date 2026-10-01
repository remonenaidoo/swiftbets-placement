SELECT TOP (@Limit) CAST(4 AS tinyint) AS Kind, p.AccountId, p.PostingId, CAST(0 AS bigint) AS Expected,
       COALESCE(SUM(e.Amount), 0) AS Actual
FROM wallet.Postings p
LEFT JOIN wallet.LedgerEntries e ON e.PostingId = p.PostingId
GROUP BY p.PostingId, p.AccountId
HAVING COALESCE(SUM(e.Amount), 0) <> 0 OR COUNT(e.EntryId) < 2
ORDER BY p.PostingId;
