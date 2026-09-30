namespace SwiftBets.Wallet.Domain;

public enum WalletFailure
{
    InsufficientFunds,
    AccountNotFound,
    AccountBlacklisted,
    ReservationNotFound,
    InvalidState,
    CurrencyMismatch,
    IdempotencyConflict,
    InvalidAmount,
}
