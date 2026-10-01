namespace SwiftBets.Wallet.Domain;

/// <summary>One broken invariant: what the ledger says it should be (<see cref="Expected"/>) against what is stored.</summary>
public sealed record LedgerDrift(DriftKind Kind, Guid? AccountId, Guid? PostingId, long Expected, long Actual);
