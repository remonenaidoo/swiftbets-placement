using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftBets.Wallet.Application.Reconciliation;
using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Infrastructure.Workers;

public sealed partial class LedgerReconciliationWorker(
    IServiceScopeFactory scopes, IOptions<ReconciliationOptions> options, TimeProvider time, ILogger<LedgerReconciliationWorker> logger) : BackgroundService
{
    private const int MaxDriftsLogged = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(options.Value.InitialDelaySeconds), time, stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.IntervalMinutes), time);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<ReconcileLedgerHandler>().HandleAsync(stoppingToken);
            Record(report);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            WalletMetrics.ReconciliationRuns.WithLabels("failed").Inc();
            LogRunFailed(ex);
        }
    }

    private void Record(ReconciliationReport report)
    {
        foreach (var kind in Enum.GetValues<DriftKind>())
        {
            WalletMetrics.ReconciliationDrifts.WithLabels(kind.ToString()).Set(report.Drifts.Count(d => d.Kind == kind));
        }

        WalletMetrics.ReconciliationRuns.WithLabels(report.IsClean ? "clean" : "drift").Inc();
        WalletMetrics.ReconciliationLastCompleted.Set(report.CompletedAt.ToUnixTimeSeconds());
        if (report.IsClean)
        {
            LogClean(report.RunId, report.AccountsChecked);
            return;
        }

        LogDriftFound(report.RunId, report.Drifts.Count, report.AccountsChecked);
        foreach (var drift in report.Drifts.Take(MaxDriftsLogged))
        {
            LogDrift(report.RunId, drift.Kind, drift.AccountId, drift.PostingId, drift.Expected, drift.Actual);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ledger reconciliation {RunId} clean across {Accounts} punter accounts")]
    private partial void LogClean(Guid runId, int accounts);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Ledger reconciliation {RunId} found {DriftCount} drifts across {Accounts} punter accounts")]
    private partial void LogDriftFound(Guid runId, int driftCount, int accounts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Ledger drift in run {RunId}: {Kind} account {AccountId} posting {PostingId} expected {Expected} actual {Actual}")]
    private partial void LogDrift(Guid runId, DriftKind kind, Guid? accountId, Guid? postingId, long expected, long actual);

    [LoggerMessage(Level = LogLevel.Error, Message = "Ledger reconciliation failed; retrying next interval")]
    private partial void LogRunFailed(Exception exception);
}
