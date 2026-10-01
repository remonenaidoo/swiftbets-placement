namespace SwiftBets.Wallet.Domain;

public sealed record ReconciliationReport(
    Guid RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int AccountsChecked,
    IReadOnlyList<LedgerDrift> Drifts)
{
    public bool IsClean => Drifts.Count == 0;
}
