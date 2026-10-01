using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Identity.Application;
using SwiftBets.Identity.Application.Accounts;
using SwiftBets.Identity.Application.Tokens;
using SwiftBets.Identity.Infrastructure.Persistence;
using SwiftBets.Identity.Infrastructure.Security;

namespace SwiftBets.Identity.Infrastructure.Tests;

/// <summary>A freshly migrated SbIdentity database with every handler wired to the real stores.</summary>
public sealed class IdentityDatabase
{
    private IdentityDatabase(string connectionString)
    {
        ConnectionString = connectionString;
        var connections = new SqlServerConnectionFactory(connectionString);
        Users = new SqlUserStore(connections);
        Tokens = new SqlTokenStore(connections);
        var options = Options.Create(Settings);
        Issuer = new RsaTokenIssuer(options, new DevelopmentHost(), Time, NullLogger<RsaTokenIssuer>.Instance);
        Register = new RegisterHandler(Users, Tokens, Hasher, Messenger, options, Time);
        Verification = new EmailVerificationHandler(Users, Tokens, Messenger, options, Time);
        Reset = new PasswordResetHandler(Users, Tokens, Hasher, Messenger, options, Time);
        Status = new ChangeStatusHandler(Users, Tokens, Time);
        Sessions = new TokenHandler(Users, Tokens, Issuer, Hasher, options, Time);
    }

    public static IdentityOptions Settings { get; } = new() { Issuer = "http://identity.test", Clients = { ["payout"] = "payout-secret" } };

    public string ConnectionString { get; }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    public RecordingMessenger Messenger { get; } = new();

    public AspNetPasswordHasher Hasher { get; } = new();

    public SqlUserStore Users { get; }

    public SqlTokenStore Tokens { get; }

    public RsaTokenIssuer Issuer { get; }

    public RegisterHandler Register { get; }

    public EmailVerificationHandler Verification { get; }

    public PasswordResetHandler Reset { get; }

    public ChangeStatusHandler Status { get; }

    public TokenHandler Sessions { get; }

    public static async Task<IdentityDatabase> CreateAsync(SqlServerFixture sql)
    {
        var connectionString = await sql.CreateDatabaseAsync("id_" + Guid.NewGuid().ToString("N")[..10]);
        (await MigrateAsync(connectionString)).ShouldBe(0);
        return new IdentityDatabase(connectionString);
    }

    public static IdentityDatabase For(string migratedConnectionString) => new(migratedConnectionString);

    public static async Task<int> MigrateAsync(string connectionString, params string[] extra)
    {
        var result = typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { $"--ConnectionStrings:SbIdentity={connectionString}" }.Concat(extra).ToArray()]);
        return result is Task<int> task ? await task : (int)result!;
    }

    private sealed class DevelopmentHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
