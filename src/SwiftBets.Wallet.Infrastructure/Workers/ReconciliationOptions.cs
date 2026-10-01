using System.ComponentModel.DataAnnotations;

namespace SwiftBets.Wallet.Infrastructure.Workers;

public sealed class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    public bool Enabled { get; set; } = true;

    /// <summary>Daily by default; each run scans the whole ledger.</summary>
    [Range(1, 10_080)]
    public int IntervalMinutes { get; set; } = 1440;

    /// <summary>The first run starts this long after the host does, so a restart reports promptly.</summary>
    [Range(0, 86_400)]
    public int InitialDelaySeconds { get; set; } = 60;
}
