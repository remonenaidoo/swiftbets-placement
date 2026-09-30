namespace SwiftBets.Wallet.Domain;

/// <summary>
/// A wallet account's balances. Punter balances can never go negative; house and funding accounts are the other side
/// of every movement and may. Methods return the balanced posting to persist, or the reason the movement is refused.
/// </summary>
public sealed class Account
{
    public Account(Guid accountId, AccountKind kind, string currency, long available, long reserved, bool isBlacklisted)
    {
        AccountId = accountId;
        Kind = kind;
        Currency = currency;
        Available = available;
        Reserved = reserved;
        IsBlacklisted = isBlacklisted;
    }

    public Guid AccountId { get; }

    public AccountKind Kind { get; }

    public string Currency { get; }

    public long Available { get; private set; }

    public long Reserved { get; private set; }

    public bool IsBlacklisted { get; }

    public WalletFailure? Reserve(long amount, string currency) =>
        Validate(amount, currency) ?? (Available < amount ? WalletFailure.InsufficientFunds : Apply(-amount, amount));

    public WalletFailure? ConsumeReservation(long amount) =>
        Reserved < amount ? WalletFailure.InvalidState : Apply(0, -amount);

    public WalletFailure? ReturnReservation(long amount) =>
        Reserved < amount ? WalletFailure.InvalidState : Apply(amount, -amount);

    public WalletFailure? Receive(long amount, string currency) =>
        Validate(amount, currency) ?? (Kind == AccountKind.Punter && IsBlacklisted ? WalletFailure.AccountBlacklisted : Apply(amount, 0));

    public WalletFailure? Pay(long amount, string currency) =>
        Validate(amount, currency) ?? (Kind == AccountKind.Punter && Available < amount ? WalletFailure.InsufficientFunds : Apply(-amount, 0));

    private WalletFailure? Validate(long amount, string currency) =>
        amount <= 0 ? WalletFailure.InvalidAmount
        : !string.Equals(currency, Currency, StringComparison.Ordinal) ? WalletFailure.CurrencyMismatch
        : null;

    private WalletFailure? Apply(long availableDelta, long reservedDelta)
    {
        Available += availableDelta;
        Reserved += reservedDelta;
        return null;
    }
}
