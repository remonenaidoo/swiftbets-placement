namespace SwiftBets.Wallet.Domain;

public sealed record LedgerEntry(Guid AccountId, Bucket Bucket, long Amount);
