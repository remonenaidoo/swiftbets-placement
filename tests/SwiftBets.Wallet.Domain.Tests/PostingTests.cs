namespace SwiftBets.Wallet.Domain.Tests;

public sealed class PostingTests
{
    [Fact]
    public void Balanced_entries_form_a_posting()
    {
        var account = Guid.NewGuid();

        var posting = new Posting(Guid.NewGuid(), PostingKind.Reserve, "k", "r", [new(account, Bucket.Available, -5), new(account, Bucket.Reserved, 5)]);

        posting.Entries.Sum(e => e.Amount).ShouldBe(0);
    }

    [Fact]
    public void Unbalanced_entries_are_rejected() =>
        Should.Throw<InvalidOperationException>(() =>
            new Posting(Guid.NewGuid(), PostingKind.Credit, "k", "r", [new(Guid.NewGuid(), Bucket.Available, 5), new(WellKnownAccounts.House, Bucket.Available, -4)]));
}
