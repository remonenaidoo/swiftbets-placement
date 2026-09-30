namespace SwiftBets.Wallet.Domain;

/// <summary>An atomic, balanced movement: its entries always sum to zero, so money is moved, never created.</summary>
public sealed record Posting
{
    public Posting(Guid postingId, PostingKind kind, string idempotencyKey, string reference, IReadOnlyList<LedgerEntry> entries)
    {
        if (entries.Count < 2 || entries.Sum(e => e.Amount) != 0)
        {
            throw new InvalidOperationException("A posting needs at least two entries that sum to zero.");
        }

        PostingId = postingId;
        Kind = kind;
        IdempotencyKey = idempotencyKey;
        Reference = reference;
        Entries = entries;
    }

    public Guid PostingId { get; }

    public PostingKind Kind { get; }

    public string IdempotencyKey { get; }

    public string Reference { get; }

    public IReadOnlyList<LedgerEntry> Entries { get; }
}
