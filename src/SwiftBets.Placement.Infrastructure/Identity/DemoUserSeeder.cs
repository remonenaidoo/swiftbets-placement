using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SwiftBets.Placement.Application.Identity;

namespace SwiftBets.Placement.Infrastructure.Identity;

/// <summary>Seeds the demo users (ids match the wallet's demo accounts) when Identity:SeedDemoUsers is on.</summary>
public sealed class DemoUserSeeder(IServiceScopeFactory scopes, IOptions<IdentityOptions> options) : IHostedService
{
    private static readonly string[] PunterRole = ["Punter"];

    public static readonly IReadOnlyList<(Guid Id, string Username, string[] Roles)> Users =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000001"), "punter1", ["Punter"]),
        (Guid.Parse("10000000-0000-0000-0000-000000000002"), "punter2", ["Punter"]),
        (Guid.Parse("10000000-0000-0000-0000-000000000003"), "punter3", ["Punter"]),
        (Guid.Parse("10000000-0000-0000-0000-000000000004"), "punter4", ["Punter"]),
        (Guid.Parse("10000000-0000-0000-0000-000000000005"), "punter5", ["Punter"]),
        (Guid.Parse("20000000-0000-0000-0000-000000000001"), "operator1", ["Operator"]),
        (Guid.Parse("30000000-0000-0000-0000-000000000001"), "admin1", ["Admin", "Operator"]),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SeedDemoUsers || string.IsNullOrEmpty(options.Value.DemoPassword))
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
        var hasher = scope.ServiceProvider.GetRequiredService<Application.Identity.IPasswordHasher>();
        var hash = hasher.Hash(options.Value.DemoPassword);
        var loadUsers = Enumerable.Range(1, options.Value.LoadTestUserCount)
            .Select(i => (Guid.Parse($"40000000-0000-0000-0000-{i:D12}"), $"load{i:D3}", PunterRole));
        foreach (var (id, username, roles) in Users.Concat(loadUsers))
        {
            await store.UpsertUserAsync(new UserRecord(id, username, hash, roles), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
