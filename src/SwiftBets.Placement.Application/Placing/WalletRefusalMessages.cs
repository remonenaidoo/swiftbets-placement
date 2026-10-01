using System.Globalization;
using System.Text.RegularExpressions;

namespace SwiftBets.Placement.Application.Placing;

/// <summary>Customer wording for wallet refusals; limit details arrive as "stake limit per day: 40000 left" in minor units.</summary>
public static partial class WalletRefusalMessages
{
    public static string For(string? code, string? detail, string currency) => code switch
    {
        "responsible_gambling_limit" when detail is not null && LimitDetail().Match(detail) is { Success: true } m =>
            $"This stake would go over your {m.Groups["limit"].Value}; {currency} {decimal.Parse(m.Groups["left"].Value, CultureInfo.InvariantCulture) / 100m:0.00} is left.",
        "responsible_gambling_limit" => "This stake would go over one of your limits.",
        "account_restricted" => "Betting is not available on this account right now.",
        "insufficient_funds" => "There is not enough in your balance for this stake.",
        _ => "The wallet refused the stake.",
    };

    [GeneratedRegex(@"^(?<limit>(stake|loss|deposit) limit per (day|week|month)): (?<left>\d+) left$")]
    private static partial Regex LimitDetail();
}
