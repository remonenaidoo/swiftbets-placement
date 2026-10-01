-- Puts back the demo punters 0005 removed, skipping any that still exist.
DECLARE @now datetimeoffset(3) = SYSUTCDATETIME();
DECLARE @punters TABLE (AccountId uniqueidentifier PRIMARY KEY);
INSERT INTO @punters VALUES
    ('10000000-0000-0000-0000-000000000001'),
    ('10000000-0000-0000-0000-000000000002'),
    ('10000000-0000-0000-0000-000000000003'),
    ('10000000-0000-0000-0000-000000000004'),
    ('10000000-0000-0000-0000-000000000005');
DELETE p FROM @punters p WHERE EXISTS (SELECT 1 FROM wallet.Accounts a WHERE a.AccountId = p.AccountId);

DECLARE @postings TABLE (PostingId uniqueidentifier PRIMARY KEY, AccountId uniqueidentifier NOT NULL);
INSERT INTO @postings (PostingId, AccountId) SELECT NEWID(), AccountId FROM @punters;

BEGIN TRANSACTION;

INSERT INTO wallet.Accounts (AccountId, Kind, Currency, Available, CreatedAt)
SELECT AccountId, 1, 'ZAR', 100000, @now FROM @punters;

INSERT INTO wallet.Postings (PostingId, Kind, IdempotencyKey, AccountId, Amount, Reference, PostedAt)
SELECT PostingId, 1, CONCAT('seed_', AccountId), AccountId, 100000, 'demo seed', @now FROM @postings;

INSERT INTO wallet.LedgerEntries (PostingId, AccountId, Bucket, Amount)
SELECT PostingId, '00000000-0000-0000-0000-00000000f001', 1, -100000 FROM @postings
UNION ALL
SELECT PostingId, AccountId, 1, 100000 FROM @postings;

COMMIT TRANSACTION;
