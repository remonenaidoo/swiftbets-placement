CREATE TABLE accounts.Users
(
    UserId          uniqueidentifier  NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Email           nvarchar(254)     NULL,
    NormalizedEmail nvarchar(254)     NULL,
    Username        nvarchar(100)     NULL,
    PasswordHash    nvarchar(500)     NOT NULL,
    DateOfBirth     date              NULL,
    Brand           varchar(20)       NOT NULL,
    Country         char(2)           NOT NULL,
    Currency        char(3)           NOT NULL,
    Status          tinyint           NOT NULL CONSTRAINT DF_Users_Status DEFAULT (0),
    EmailVerifiedAt datetimeoffset(3) NULL,
    FailedSignIns   int               NOT NULL CONSTRAINT DF_Users_FailedSignIns DEFAULT (0),
    LockedUntil     datetimeoffset(3) NULL,
    ParentUserId    uniqueidentifier  NULL CONSTRAINT FK_Users_Parent REFERENCES accounts.Users (UserId),
    CreatedAt       datetimeoffset(3) NOT NULL,
    UpdatedAt       datetimeoffset(3) NOT NULL,
    CONSTRAINT CK_Users_Login CHECK (NormalizedEmail IS NOT NULL OR Username IS NOT NULL)
);
CREATE UNIQUE INDEX UX_Users_NormalizedEmail ON accounts.Users (NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;
CREATE UNIQUE INDEX UX_Users_Username ON accounts.Users (Username) WHERE Username IS NOT NULL;

CREATE TABLE accounts.UserRoles
(
    UserId uniqueidentifier NOT NULL CONSTRAINT FK_UserRoles_Users REFERENCES accounts.Users (UserId),
    Role   varchar(30)      NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, Role)
);

CREATE TABLE accounts.RolePermissions
(
    Role       varchar(30) NOT NULL,
    Permission varchar(80) NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY (Role, Permission)
);

-- Every status change, who made it and why: the account half of the audit trail.
CREATE TABLE accounts.StatusHistory
(
    StatusChangeId bigint IDENTITY(1, 1) NOT NULL CONSTRAINT PK_StatusHistory PRIMARY KEY,
    UserId         uniqueidentifier  NOT NULL CONSTRAINT FK_StatusHistory_Users REFERENCES accounts.Users (UserId),
    PreviousStatus tinyint           NOT NULL,
    Status         tinyint           NOT NULL,
    Reason         nvarchar(500)     NOT NULL,
    ChangedBy      nvarchar(100)     NOT NULL,
    ChangedAt      datetimeoffset(3) NOT NULL
);
CREATE INDEX IX_StatusHistory_User ON accounts.StatusHistory (UserId, ChangedAt);

CREATE TABLE accounts.RefreshTokens
(
    TokenHash  binary(32)        NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId     uniqueidentifier  NOT NULL CONSTRAINT FK_RefreshTokens_Users REFERENCES accounts.Users (UserId),
    FamilyId   uniqueidentifier  NOT NULL,
    ExpiresAt  datetimeoffset(3) NOT NULL,
    ConsumedAt datetimeoffset(3) NULL,
    RevokedAt  datetimeoffset(3) NULL
);
CREATE INDEX IX_RefreshTokens_Family ON accounts.RefreshTokens (FamilyId);
CREATE INDEX IX_RefreshTokens_User ON accounts.RefreshTokens (UserId) WHERE RevokedAt IS NULL;

CREATE TABLE accounts.OneTimeTokens
(
    TokenHash binary(32)        NOT NULL CONSTRAINT PK_OneTimeTokens PRIMARY KEY,
    UserId    uniqueidentifier  NOT NULL CONSTRAINT FK_OneTimeTokens_Users REFERENCES accounts.Users (UserId),
    Purpose   tinyint           NOT NULL,
    ExpiresAt datetimeoffset(3) NOT NULL,
    UsedAt    datetimeoffset(3) NULL
);
