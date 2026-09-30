namespace SwiftBets.Wallet.Domain;

public static class WellKnownAccounts
{
    /// <summary>The bookmaker: captured stakes land here and winnings are paid from here.</summary>
    public static readonly Guid House = Guid.Parse("00000000-0000-0000-0000-00000000b001");

    /// <summary>The outside world: top-ups come from here.</summary>
    public static readonly Guid Funding = Guid.Parse("00000000-0000-0000-0000-00000000f001");
}
