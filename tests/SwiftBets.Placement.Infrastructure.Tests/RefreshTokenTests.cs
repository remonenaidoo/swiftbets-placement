using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Placement.Application.Identity;
using SwiftBets.Placement.Infrastructure.Identity;

namespace SwiftBets.Placement.Infrastructure.Tests;

public sealed class RefreshTokenTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Refresh_token_rotates_into_a_new_pair()
    {
        var (tokens, first) = await SignedInAsync();

        var second = await tokens.RefreshAsync(first.RefreshToken!, CancellationToken.None);

        second.IsSuccess.ShouldBeTrue();
        second.Value.RefreshToken.ShouldNotBe(first.RefreshToken);
    }

    [Fact]
    public async Task Reusing_a_consumed_refresh_token_revokes_the_whole_family()
    {
        var (tokens, first) = await SignedInAsync();
        var second = await tokens.RefreshAsync(first.RefreshToken!, CancellationToken.None);

        (await tokens.RefreshAsync(first.RefreshToken!, CancellationToken.None)).Error!.Code.ShouldBe("refresh_token_reused");
        (await tokens.RefreshAsync(second.Value.RefreshToken!, CancellationToken.None)).IsFailure.ShouldBeTrue();
    }

    private async Task<(TokenHandler Tokens, TokenResponse First)> SignedInAsync()
    {
        var connectionString = await sql.CreateDatabaseAsync("identity_" + Guid.NewGuid().ToString("N")[..10]);
        var entry = typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { $"--ConnectionStrings:SbPlacement={connectionString}" }]);
        (entry is Task<int> task ? await task : (int)entry!).ShouldBe(0);

        var options = Options.Create(new IdentityOptions { Issuer = "https://issuer.test" });
        var store = new SqlIdentityStore(new SqlServerConnectionFactory(connectionString));
        var hasher = new AspNetPasswordHasher();
        await store.UpsertUserAsync(new UserRecord(Guid.NewGuid(), "punter", hasher.Hash("pass-word-1"), ["Punter"]), CancellationToken.None);
        var issuer = new RsaTokenIssuer(options, new HostingEnvironment { EnvironmentName = "Development" }, TimeProvider.System, NullLogger<RsaTokenIssuer>.Instance);
        var tokens = new TokenHandler(store, issuer, hasher, options, TimeProvider.System);
        return (tokens, (await tokens.PasswordAsync("punter", "pass-word-1", CancellationToken.None)).Value);
    }
}
