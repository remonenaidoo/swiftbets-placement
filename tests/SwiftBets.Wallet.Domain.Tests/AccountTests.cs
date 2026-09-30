namespace SwiftBets.Wallet.Domain.Tests;

public sealed class AccountTests
{
    [Fact]
    public void Reserving_moves_funds_from_available_to_reserved()
    {
        var account = new Account(Guid.NewGuid(), AccountKind.Punter, "ZAR", available: 1_000, reserved: 0, isBlacklisted: false);

        account.Reserve(400, "ZAR").ShouldBeNull();

        (account.Available, account.Reserved).ShouldBe((600L, 400L));
    }

    [Fact]
    public void Reserving_more_than_available_is_refused_and_changes_nothing()
    {
        var account = new Account(Guid.NewGuid(), AccountKind.Punter, "ZAR", available: 100, reserved: 0, isBlacklisted: false);

        account.Reserve(101, "ZAR").ShouldBe(WalletFailure.InsufficientFunds);

        account.Available.ShouldBe(100);
    }
}
