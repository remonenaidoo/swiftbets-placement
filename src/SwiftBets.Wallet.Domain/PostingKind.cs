namespace SwiftBets.Wallet.Domain;

public enum PostingKind : byte
{
    TopUp = 1,
    Reserve = 2,
    Capture = 3,
    Release = 4,
    Credit = 5,
    Debit = 6,
}
