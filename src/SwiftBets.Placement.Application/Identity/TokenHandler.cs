using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;

namespace SwiftBets.Placement.Application.Identity;

/// <summary>Password and client-credentials grants, and rotating single-use refresh tokens.</summary>
public sealed class TokenHandler(IIdentityStore store, ITokenIssuer issuer, IPasswordHasher hasher, IOptions<IdentityOptions> options, TimeProvider time)
{
    private static readonly Error InvalidCredentials = new("invalid_credentials", "Username or password is incorrect.", ErrorKind.Unauthorized);

    public async Task<Result<TokenResponse>> PasswordAsync(string username, string password, CancellationToken cancellationToken)
    {
        var user = await store.FindByUsernameAsync(username, cancellationToken);
        if (user is null || !hasher.Verify(user.PasswordHash, password))
        {
            return InvalidCredentials;
        }

        return Result.Success(await IssueAsync(user, Guid.CreateVersion7()));
    }

    public Result<TokenResponse> ClientCredentials(string clientId, string clientSecret)
    {
        if (!options.Value.Clients.TryGetValue(clientId, out var expected)
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret))))
        {
            return new Error("invalid_client", "Unknown client or wrong secret.", ErrorKind.Unauthorized);
        }

        var lifetime = TimeSpan.FromMinutes(options.Value.AccessTokenMinutes);
        return Result.Success(new TokenResponse(issuer.IssueAccessToken($"client:{clientId}", ["Service"], lifetime), "Bearer", (int)lifetime.TotalSeconds, null));
    }

    public async Task<Result<TokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var (outcome, userId, familyId) = await store.ConsumeRefreshTokenAsync(Hash(refreshToken), time.GetUtcNow());
        if (outcome != RefreshOutcome.Valid)
        {
            return new Error(outcome == RefreshOutcome.Reused ? "refresh_token_reused" : "invalid_refresh_token", "Sign in again.", ErrorKind.Unauthorized);
        }

        var user = await store.FindByIdAsync(userId, cancellationToken);
        return user is null ? InvalidCredentials : Result.Success(await IssueAsync(user, familyId));
    }

    private async Task<TokenResponse> IssueAsync(UserRecord user, Guid familyId)
    {
        var lifetime = TimeSpan.FromMinutes(options.Value.AccessTokenMinutes);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        await store.StoreRefreshTokenAsync(Hash(refresh), user.UserId, familyId, time.GetUtcNow().AddDays(options.Value.RefreshTokenDays));
        return new TokenResponse(issuer.IssueAccessToken(user.UserId.ToString(), user.Roles, lifetime), "Bearer", (int)lifetime.TotalSeconds, refresh);
    }

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
