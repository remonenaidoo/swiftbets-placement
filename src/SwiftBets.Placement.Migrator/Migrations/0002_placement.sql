IF SCHEMA_ID(N'auth') IS NULL EXEC (N'CREATE SCHEMA auth');

CREATE TABLE auth.Users
(
    UserId       uniqueidentifier  NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Username     nvarchar(100)     NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
    PasswordHash nvarchar(500)     NOT NULL,
    Roles        nvarchar(200)     NOT NULL,
    CreatedAt    datetimeoffset(3) NOT NULL
);

CREATE TABLE auth.RefreshTokens
(
    TokenHash  binary(32)        NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId     uniqueidentifier  NOT NULL CONSTRAINT FK_RefreshTokens_Users REFERENCES auth.Users (UserId),
    FamilyId   uniqueidentifier  NOT NULL,
    ExpiresAt  datetimeoffset(3) NOT NULL,
    ConsumedAt datetimeoffset(3) NULL,
    RevokedAt  datetimeoffset(3) NULL
);
CREATE INDEX IX_RefreshTokens_Family ON auth.RefreshTokens (FamilyId);

CREATE TABLE placement.SagaIntents
(
    CouponId       uniqueidentifier  NOT NULL CONSTRAINT PK_SagaIntents PRIMARY KEY,
    PunterId       uniqueidentifier  NOT NULL,
    IdempotencyKey nvarchar(200)     NOT NULL,
    RequestHash    char(64)          NOT NULL,
    Stake          bigint            NOT NULL,
    Currency       char(3)           NOT NULL,
    State          tinyint           NOT NULL,
    ReservationId  uniqueidentifier  NULL,
    ResponseStatus int               NULL,
    ResponseJson   nvarchar(max)     NULL,
    CreatedAt      datetimeoffset(3) NOT NULL,
    UpdatedAt      datetimeoffset(3) NOT NULL,
    DeadlineAt     datetimeoffset(3) NOT NULL,
    LeaseUntil     datetimeoffset(3) NULL,
    SweepAttempts  int               NOT NULL CONSTRAINT DF_SagaIntents_SweepAttempts DEFAULT (0),
    CONSTRAINT UQ_SagaIntents_PunterKey UNIQUE (PunterId, IdempotencyKey)
);
CREATE INDEX IX_SagaIntents_Unfinished ON placement.SagaIntents (DeadlineAt) INCLUDE (State, LeaseUntil) WHERE State IN (1, 2, 3, 7);

CREATE TABLE placement.Coupons
(
    CouponId        uniqueidentifier  NOT NULL CONSTRAINT PK_Coupons PRIMARY KEY CONSTRAINT FK_Coupons_SagaIntents REFERENCES placement.SagaIntents (CouponId),
    PunterId        uniqueidentifier  NOT NULL,
    BetType         tinyint           NOT NULL,
    Stake           bigint            NOT NULL CONSTRAINT CK_Coupons_Stake CHECK (Stake > 0),
    Currency        char(3)           NOT NULL,
    TotalOdds       decimal(28, 6)    NOT NULL,
    PotentialPayout bigint            NOT NULL,
    IsOpen          bit               NOT NULL CONSTRAINT DF_Coupons_IsOpen DEFAULT (1),
    PlacedAt        datetimeoffset(3) NOT NULL
);
CREATE INDEX IX_Coupons_Punter ON placement.Coupons (PunterId, PlacedAt DESC);

CREATE TABLE placement.CouponLegs
(
    LegId        uniqueidentifier NOT NULL CONSTRAINT PK_CouponLegs PRIMARY KEY,
    CouponId     uniqueidentifier NOT NULL CONSTRAINT FK_CouponLegs_Coupons REFERENCES placement.Coupons (CouponId),
    FixtureId    nvarchar(100)    NOT NULL,
    MarketId     nvarchar(120)    NOT NULL,
    SelectionId  nvarchar(50)     NOT NULL,
    Odds         decimal(10, 3)   NOT NULL CONSTRAINT CK_CouponLegs_Odds CHECK (Odds >= 1.01),
    OfferVersion bigint           NOT NULL
);
CREATE INDEX IX_CouponLegs_Fixture ON placement.CouponLegs (FixtureId) INCLUDE (CouponId);

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::auth TO swiftbets_app;
