using Prometheus;

namespace SwiftBets.Wallet.Infrastructure;

/// <summary>What the ledger-drift alert reads.</summary>
public static class WalletMetrics
{
    public static readonly Gauge ReconciliationDrifts = Metrics.CreateGauge(
        "swiftbets_wallet_reconciliation_drifts", "Drifts found by the latest ledger reconciliation, by invariant.", new GaugeConfiguration { LabelNames = ["kind"] });

    public static readonly Counter ReconciliationRuns = Metrics.CreateCounter(
        "swiftbets_wallet_reconciliation_runs_total", "Ledger reconciliation runs, by outcome (clean, drift, failed).", new CounterConfiguration { LabelNames = ["outcome"] });

    public static readonly Gauge ReconciliationLastCompleted = Metrics.CreateGauge(
        "swiftbets_wallet_reconciliation_last_completed_timestamp_seconds", "Unix time the latest reconciliation run completed, clean or not.");
}
