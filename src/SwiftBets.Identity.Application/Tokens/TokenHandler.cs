using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;
using SwiftBets.Identity.Application.Ports;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Application.Tokens;

/// <summary>
/// Password and client-credentials grants, and rotating single-use refresh tokens. A wrong password counts towards a
/// temporary lockout; an account that is not active is told why only after its password is proven, and cannot refresh.
/// </summary>
public sealed class TokenHandler(IUserStore users, ITokenStore tokens, ITokenIssuer issuer, IPasswordHasher hasher, IOptions<IdentityOptions> options, TimeProvider time)
{
    private static readonly Error InvalidCredentials = new("invalid_credentials", "Email or password is incorrect.", ErrorKind.Unauthorized);

    // Verified against when the login is unknown, so a miss costs the same time as a wrong password.
    private readonly Lazy<string> _decoyHash = new(() => hasher.Hash(Guid.NewGuid().ToString()));

    public async Task<Result<TokenResponse>> PasswordAsync(string login, string password, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var user = await users.FindByLoginAsync(Login(login), cancellationToken);
        if (user is null)
        {
            hasher.Verify(_decoyHash.Value, password);
            return InvalidCredentials;
        }

        if (user.IsLockedAt(now))
        {
            return new Error("account_locked", "Too many failed attempts. Try again in a few minutes or reset your password.", ErrorKind.Unauthorized);
        }

        if (!hasher.Verify(user.PasswordHash, password))
        {
            await users.RecordFailedSignInAsync(user.UserId, now);
            return InvalidCredentials;
        }

        if (user.FailedSignIns > 0)
        {
            await users.ClearFailedSignInsAsync(user.UserId);
        }

        if (user.SignInRefusal(now) is { } refusal)
        {
            return new Error(refusal, "This account cannot sign in. Contact support.", ErrorKind.Forbidden);
        }

        return Result.Success(await IssueAsync(user, Guid.CreateVersion7(), cancellationToken));
    }

    public Result<TokenResponse> ClientCredentials(string clientId, string clientSecret)
    {
        if (!options.Value.Clients.TryGetValue(clientId, out var expected)
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret))))
        {
            return new Error("invalid_client", "Unknown client or wrong secret.", ErrorKind.Unauthorized);
        }

        var lifetime = TimeSpan.FromMinutes(options.Value.AccessTokenMinutes);
        return Result.Success(new TokenResponse(issuer.IssueAccessToken($"client:{clientId}", [RoleNames.Service], [], lifetime), "Bearer", (int)lifetime.TotalSeconds, null));
    }

    public async Task<Result<TokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var hash = SecretTokens.Hash(refreshToken);
        var (outcome, userId, familyId) = await tokens.ConsumeRefreshTokenAsync(hash, now);
        if (outcome != RefreshOutcome.Valid)
        {
            return new Error(outcome == RefreshOutcome.Reused ? "refresh_token_reused" : "invalid_refresh_token", "Sign in again.", ErrorKind.Unauthorized);
        }

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
        {
            await tokens.RevokeAllForUserAsync(userId, now);
            return new Error("invalid_refresh_token", "Sign in again.", ErrorKind.Unauthorized);
        }

        return Result.Success(await IssueAsync(user, familyId, cancellationToken));
    }

    /// <summary>Signs a device out: its refresh token and every token rotated from it stop working.</summary>
    public Task RevokeAsync(string refreshToken) => tokens.RevokeFamilyAsync(SecretTokens.Hash(refreshToken), time.GetUtcNow());

    private async Task<TokenResponse> IssueAsync(User user, Guid familyId, CancellationToken cancellationToken)
    {
        var lifetime = TimeSpan.FromMinutes(options.Value.AccessTokenMinutes);
        var refresh = SecretTokens.New();
        await tokens.StoreRefreshTokenAsync(SecretTokens.Hash(refresh), user.UserId, familyId, time.GetUtcNow().AddDays(options.Value.RefreshTokenDays));
        var roles = user.Roles.Select(RoleNames.Canonical).ToList();
        var permissions = roles.Any(RoleNames.IsStaff) ? await users.PermissionsForAsync(roles, cancellationToken) : [];
        return new TokenResponse(issuer.IssueAccessToken(user.UserId.ToString(), RoleNames.Claims(roles), permissions, lifetime), "Bearer", (int)lifetime.TotalSeconds, refresh);
    }

    private static string Login(string login) => login.Contains('@', StringComparison.Ordinal) ? EmailAddress.Normalize(login) : login.Trim();
}
