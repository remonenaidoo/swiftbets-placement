-- 0003 funded demo punters in every deployment; the seed now runs only with Migrator:SeedDemo. Untouched seed rows go.
DECLARE @seeded TABLE (AccountId uniqueidentifier PRIMARY KEY, PostingId uniqueidentifier NOT NULL);

INSERT INTO @seeded (AccountId, PostingId)
SELECT p.AccountId, p.PostingId
FROM wallet.Postings p
JOIN wallet.Accounts a ON a.AccountId = p.AccountId
WHERE p.IdempotencyKey = CONCAT('seed_', p.AccountId)
  AND a.Kind = 1 AND a.Available = 100000 AND a.Reserved = 0
  AND NOT EXISTS (SELECT 1 FROM wallet.Postings o WHERE o.AccountId = p.AccountId AND o.PostingId <> p.PostingId)
  AND NOT EXISTS (SELECT 1 FROM wallet.Reservations r WHERE r.AccountId = p.AccountId);

DELETE e FROM wallet.LedgerEntries e JOIN @seeded s ON s.PostingId = e.PostingId;
DELETE p FROM wallet.Postings p JOIN @seeded s ON s.PostingId = p.PostingId;
DELETE a FROM wallet.Accounts a JOIN @seeded s ON s.AccountId = a.AccountId;
