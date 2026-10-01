-- V2 coupons: legs may be bankers, and a coupon holds one or more bets (fold sizes over its non-banker legs).
ALTER TABLE placement.CouponLegs ADD IsBanker bit NOT NULL CONSTRAINT DF_CouponLegs_IsBanker DEFAULT (0);

CREATE TABLE placement.CouponBets
(
    BetId           uniqueidentifier NOT NULL CONSTRAINT PK_CouponBets PRIMARY KEY,
    CouponId        uniqueidentifier NOT NULL CONSTRAINT FK_CouponBets_Coupons REFERENCES placement.Coupons (CouponId),
    Name            nvarchar(40)     NOT NULL,
    Folds           varchar(60)      NOT NULL,
    Lines           int              NOT NULL CONSTRAINT CK_CouponBets_Lines CHECK (Lines > 0),
    UnitStake       bigint           NOT NULL CONSTRAINT CK_CouponBets_UnitStake CHECK (UnitStake > 0),
    Stake           bigint           NOT NULL,
    PotentialPayout bigint           NOT NULL
);
CREATE INDEX IX_CouponBets_Coupon ON placement.CouponBets (CouponId);
GO

-- Coupons placed before V2 are each one bet: every leg in one line, the whole stake on it.
INSERT INTO placement.CouponBets (BetId, CouponId, Name, Folds, Lines, UnitStake, Stake, PotentialPayout)
SELECT NEWID(), c.CouponId, CASE WHEN l.Legs = 1 THEN N'single' ELSE N'accumulator' END, CAST(l.Legs AS varchar(10)), 1, c.Stake, c.Stake, c.PotentialPayout
FROM placement.Coupons c
CROSS APPLY (SELECT COUNT(*) AS Legs FROM placement.CouponLegs WHERE CouponId = c.CouponId) l
WHERE NOT EXISTS (SELECT 1 FROM placement.CouponBets b WHERE b.CouponId = c.CouponId);
