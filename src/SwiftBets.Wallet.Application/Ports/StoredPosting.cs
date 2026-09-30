using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ports;

/// <summary>What an idempotency key already produced; a repeat must match it exactly or it is a conflict.</summary>
public sealed record StoredPosting(Guid PostingId, PostingKind Kind, Guid AccountId, long Amount, Guid? ReservationId);
