using SwiftBets.Placement.Application.Placing;

namespace SwiftBets.Placement.Application.Tests;

public sealed class WalletRefusalMessagesTests
{
    [Theory]
    [InlineData("responsible_gambling_limit", "stake limit per day: 40000 left", "This stake would go over your stake limit per day; ZAR 400.00 is left.")]
    [InlineData("responsible_gambling_limit", "loss limit per month: 5 left", "This stake would go over your loss limit per month; ZAR 0.05 is left.")]
    [InlineData("responsible_gambling_limit", "something unexpected", "This stake would go over one of your limits.")]
    [InlineData("account_restricted", "account excluded", "Betting is not available on this account right now.")]
    [InlineData("insufficient_funds", null, "There is not enough in your balance for this stake.")]
    [InlineData(null, null, "The wallet refused the stake.")]
    public void Refusals_read_as_customer_wording(string? code, string? detail, string expected) =>
        WalletRefusalMessages.For(code, detail, "ZAR").ShouldBe(expected);
}
