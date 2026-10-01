using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ports;

/// <summary>
/// Read-only checks of the ledger against its balance projections. Each check is one statement, so under read committed
/// snapshot it sees a single committed state and an in-flight posting can never show up as drift.
/// </summary>
public interface IReconciliationStore
{
    Task<int> CountPunterAccountsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerDrift>> FindBalanceDriftAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerDrift>> FindReservationDriftAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerDrift>> FindUnbalancedPostingsAsync(CancellationToken cancellationToken);

    Task<long> SumAllEntriesAsync(CancellationToken cancellationToken);

    Task SaveAsync(ReconciliationReport report, CancellationToken cancellationToken);

    Task<ReconciliationReport?> GetLatestAsync(CancellationToken cancellationToken);
}
