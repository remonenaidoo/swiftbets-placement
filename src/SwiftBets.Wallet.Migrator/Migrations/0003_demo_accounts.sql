-- Demo punters (ids match the identity seed), each funded with R1 000.00 through a balanced top-up posting.
DECLARE @now datetimeoffset(3) = SYSUTCDATETIME();
DECLARE @punters TABLE (AccountId uniqueidentifier);
INSERT INTO @punters VALUES
    ('10000000-0000-0000-0000-000000000001'),
    ('10000000-0000-0000-0000-000000000002'),
    ('10000000-0000-0000-0000-000000000003'),
    ('10000000-0000-0000-0000-000000000004'),
    ('10000000-0000-0000-0000-000000000005');

INSERT INTO wallet.Accounts (AccountId, Kind, Currency, Available, CreatedAt)
SELECT AccountId, 1, 'ZAR', 100000, @now FROM @punters;

INSERT INTO wallet.Postings (PostingId, Kind, IdempotencyKey, AccountId, Amount, Reference, PostedAt)
SELECT NEWID(), 1, CONCAT('seed_', AccountId), AccountId, 100000, 'demo seed', @now FROM @punters;

INSERT INTO wallet.LedgerEntries (PostingId, AccountId, Bucket, Amount)
SELECT p.PostingId, '00000000-0000-0000-0000-00000000f001', 1, -100000 FROM wallet.Postings p WHERE p.IdempotencyKey LIKE 'seed[_]%'
UNION ALL
SELECT p.PostingId, p.AccountId, 1, 100000 FROM wallet.Postings p WHERE p.IdempotencyKey LIKE 'seed[_]%';
