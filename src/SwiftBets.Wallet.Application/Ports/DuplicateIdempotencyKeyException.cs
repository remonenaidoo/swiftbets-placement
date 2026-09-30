namespace SwiftBets.Wallet.Application.Ports;

/// <summary>Raised by the store when a concurrent request committed the same idempotency key first.</summary>
public sealed class DuplicateIdempotencyKeyException(string idempotencyKey, Exception innerException)
    : Exception($"Idempotency key '{idempotencyKey}' was committed concurrently.", innerException)
{
    public string IdempotencyKey { get; } = idempotencyKey;
}
