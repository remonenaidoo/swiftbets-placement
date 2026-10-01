namespace SwiftBets.Identity.Domain;

/// <summary>
/// Roles and the token claims they carry. The pre-product roles (Punter, Operator) are still emitted alongside the new
/// ones for one release (D100), because every service authorises on them today.
/// </summary>
public static class RoleNames
{
    public const string Customer = "Customer";
    public const string Agent = "Agent";
    public const string Trader = "Trader";
    public const string Ops = "Ops";
    public const string Admin = "Admin";
    public const string Service = "Service";

    public const string LegacyPunter = "Punter";
    public const string LegacyOperator = "Operator";

    public static readonly IReadOnlyList<string> All = [Customer, Agent, Trader, Ops, Admin];

    public static bool IsStaff(string role) => role is Trader or Ops or Admin;

    /// <summary>The role claims a token carries for these roles, legacy names included.</summary>
    public static IReadOnlyList<string> Claims(IEnumerable<string> roles)
    {
        var claims = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            claims.Add(Canonical(role));
            if (Canonical(role) == Customer)
            {
                claims.Add(LegacyPunter);
            }
            else if (IsStaff(Canonical(role)))
            {
                claims.Add(LegacyOperator);
            }
        }

        return [.. claims];
    }

    /// <summary>Maps a stored legacy role to its product role; product roles map to themselves.</summary>
    public static string Canonical(string role) => role switch
    {
        LegacyPunter => Customer,
        LegacyOperator => Ops,
        _ => role,
    };
}
