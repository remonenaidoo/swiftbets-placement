extern alias migrator;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Identity.Application.Ports;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace SwiftBets.Identity.Api.Tests;

/// <summary>The identity host on a freshly migrated database, with account emails captured instead of sent.</summary>
public sealed class IdentityHost : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Demo-Password-1";

    private readonly string _connectionString;

    private IdentityHost(string connectionString) => _connectionString = connectionString;

    public CapturingMessenger Emails { get; } = new();

    public static async Task<IdentityHost> StartAsync(SqlServerFixture sql)
    {
        var connectionString = await sql.CreateDatabaseAsync("idh_" + Guid.NewGuid().ToString("N")[..10]);
        MigrationRunner.RunSqlServer(connectionString, false, new MigrationSource(typeof(migrator::Program).Assembly, 1)).Successful.ShouldBeTrue();
        return new IdentityHost(connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:SbIdentity", _connectionString);
        builder.UseSetting("Identity:Issuer", "http://identity.test");
        builder.UseSetting("Identity:SeedDemoUsers", "true");
        builder.UseSetting("Identity:DemoPassword", DemoPassword);
        builder.UseSetting("Jwt:Authority", "http://identity.test");
        builder.UseSetting("Jwt:RequireHttpsMetadata", "false");
        builder.ConfigureTestServices(services => services.AddSingleton<IAccountMessenger>(Emails));
    }

    public sealed class CapturingMessenger : IAccountMessenger
    {
        public ConcurrentQueue<(string Kind, string Email, string? Token)> Sent { get; } = new();

        public string LastToken(string kind) => Sent.Last(s => s.Kind == kind).Token!;

        public Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken) => Record("verify", email, token);

        public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken) => Record("reset", email, token);

        public Task SendAlreadyRegisteredAsync(string email, CancellationToken cancellationToken) => Record("already-registered", email, null);

        private Task Record(string kind, string email, string? token)
        {
            Sent.Enqueue((kind, email, token));
            return Task.CompletedTask;
        }
    }
}
