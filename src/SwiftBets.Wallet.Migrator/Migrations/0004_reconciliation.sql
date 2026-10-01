-- History of ledger reconciliation runs and the drifts each one found. Diagnostic data only; no money lives here.
CREATE TABLE wallet.ReconciliationRuns
(
    RunId           uniqueidentifier  NOT NULL CONSTRAINT PK_ReconciliationRuns PRIMARY KEY NONCLUSTERED,
    Sequence        bigint IDENTITY(1, 1) NOT NULL,
    StartedAt       datetimeoffset(3) NOT NULL,
    CompletedAt     datetimeoffset(3) NOT NULL,
    AccountsChecked int               NOT NULL,
    DriftCount      int               NOT NULL
);
CREATE CLUSTERED INDEX CX_ReconciliationRuns_Sequence ON wallet.ReconciliationRuns (Sequence);

CREATE TABLE wallet.ReconciliationDrifts
(
    DriftId   bigint IDENTITY(1, 1) NOT NULL CONSTRAINT PK_ReconciliationDrifts PRIMARY KEY,
    RunId     uniqueidentifier NOT NULL CONSTRAINT FK_ReconciliationDrifts_Runs REFERENCES wallet.ReconciliationRuns (RunId),
    Kind      tinyint          NOT NULL,
    AccountId uniqueidentifier NULL,
    PostingId uniqueidentifier NULL,
    Expected  bigint           NOT NULL,
    Actual    bigint           NOT NULL
);
CREATE INDEX IX_ReconciliationDrifts_Run ON wallet.ReconciliationDrifts (RunId);
