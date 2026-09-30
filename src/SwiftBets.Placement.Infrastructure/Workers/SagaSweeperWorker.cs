using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Application.Sweeping;

namespace SwiftBets.Placement.Infrastructure.Workers;

public sealed partial class SagaSweeperWorker(IServiceScopeFactory scopes, IOptions<PlacementOptions> options, TimeProvider time, ILogger<SagaSweeperWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SweepOrphansHandler>().SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogSweepFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Saga sweep failed; retrying next interval")]
    private partial void LogSweepFailed(Exception exception);
}
