using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SwiftBets.Identity.Application;
using SwiftBets.Identity.Application.Ports;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Infrastructure.Persistence;

/// <summary>Dev and preview only: the demo accounts (ids match the wallet's demo accounts) and optional load-test punters.</summary>
public sealed class DemoUserSeeder(IServiceScopeFactory scopes, IOptions<IdentityOptions> options, TimeProvider time) : IHostedService
{
    public static readonly IReadOnlyList<(Guid Id, string Username, string Role)> Users =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000001"), "punter1", RoleNames.Customer),
        (Guid.Parse("10000000-0000-0000-0000-000000000002"), "punter2", RoleNames.Customer),
        (Guid.Parse("10000000-0000-0000-0000-000000000003"), "punter3", RoleNames.Customer),
        (Guid.Parse("10000000-0000-0000-0000-000000000004"), "punter4", RoleNames.Customer),
        (Guid.Parse("10000000-0000-0000-0000-000000000005"), "punter5", RoleNames.Customer),
        (Guid.Parse("20000000-0000-0000-0000-000000000001"), "operator1", RoleNames.Ops),
        (Guid.Parse("30000000-0000-0000-0000-000000000001"), "admin1", RoleNames.Admin),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SeedDemoUsers || string.IsNullOrEmpty(options.Value.DemoPassword))
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(options.Value.DemoPassword);
        var loadUsers = Enumerable.Range(1, options.Value.LoadTestUserCount)
            .Select(i => (Guid.Parse($"40000000-0000-0000-0000-{i:D12}"), $"load{i:D3}", RoleNames.Customer));
        foreach (var (id, username, role) in Users.Concat(loadUsers))
        {
            await store.UpsertSeedUserAsync(new User(id, $"{username}@demo.swiftbets.local", username, hash, new DateOnly(1990, 1, 1), options.Value.Brand,
                "ZA", "ZAR", AccountStatus.Active, time.GetUtcNow(), 0, null, null, [role]), time.GetUtcNow());
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
