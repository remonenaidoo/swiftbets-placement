CREATE TABLE wallet.Accounts
(
    AccountId     uniqueidentifier  NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    Kind          tinyint           NOT NULL,
    Currency      char(3)           NOT NULL,
    Available     bigint            NOT NULL CONSTRAINT DF_Accounts_Available DEFAULT (0),
    Reserved      bigint            NOT NULL CONSTRAINT DF_Accounts_Reserved DEFAULT (0),
    IsBlacklisted bit               NOT NULL CONSTRAINT DF_Accounts_IsBlacklisted DEFAULT (0),
    CreatedAt     datetimeoffset(3) NOT NULL,
    CONSTRAINT CK_Accounts_PunterNotNegative CHECK (Kind <> 1 OR (Available >= 0 AND Reserved >= 0))
);

CREATE TABLE wallet.Postings
(
    PostingId      uniqueidentifier  NOT NULL CONSTRAINT PK_Postings PRIMARY KEY NONCLUSTERED,
    Sequence       bigint IDENTITY(1, 1) NOT NULL,
    Kind           tinyint           NOT NULL,
    IdempotencyKey nvarchar(200)     NOT NULL,
    AccountId      uniqueidentifier  NOT NULL,
    Amount         bigint            NOT NULL,
    ReservationId  uniqueidentifier  NULL,
    Reference      nvarchar(200)     NOT NULL,
    PostedAt       datetimeoffset(3) NOT NULL,
    CONSTRAINT UQ_Postings_IdempotencyKey UNIQUE (IdempotencyKey)
);
CREATE CLUSTERED INDEX CX_Postings_Sequence ON wallet.Postings (Sequence);

-- Append-only: every posting's entries sum to zero; balances are projections of these rows.
CREATE TABLE wallet.LedgerEntries
(
    EntryId   bigint IDENTITY(1, 1) NOT NULL CONSTRAINT PK_LedgerEntries PRIMARY KEY,
    PostingId uniqueidentifier NOT NULL CONSTRAINT FK_LedgerEntries_Postings REFERENCES wallet.Postings (PostingId),
    AccountId uniqueidentifier NOT NULL CONSTRAINT FK_LedgerEntries_Accounts REFERENCES wallet.Accounts (AccountId),
    Bucket    tinyint          NOT NULL,
    Amount    bigint           NOT NULL
);
CREATE INDEX IX_LedgerEntries_Account ON wallet.LedgerEntries (AccountId, Bucket) INCLUDE (Amount);

CREATE TABLE wallet.Reservations
(
    ReservationId uniqueidentifier  NOT NULL CONSTRAINT PK_Reservations PRIMARY KEY,
    AccountId     uniqueidentifier  NOT NULL CONSTRAINT FK_Reservations_Accounts REFERENCES wallet.Accounts (AccountId),
    Amount        bigint            NOT NULL CONSTRAINT CK_Reservations_Amount CHECK (Amount > 0),
    Currency      char(3)           NOT NULL,
    Reference     nvarchar(200)     NOT NULL,
    State         tinyint           NOT NULL,
    CreatedAt     datetimeoffset(3) NOT NULL,
    UpdatedAt     datetimeoffset(3) NOT NULL
);

INSERT INTO wallet.Accounts (AccountId, Kind, Currency, CreatedAt) VALUES
    ('00000000-0000-0000-0000-00000000b001', 2, 'ZAR', SYSUTCDATETIME()),
    ('00000000-0000-0000-0000-00000000f001', 3, 'ZAR', SYSUTCDATETIME());
