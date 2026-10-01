using Microsoft.Extensions.Options;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;
using SwiftBets.Identity.Application.Ports;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Application.Accounts;

/// <summary>Reset by emailed single-use link. A completed reset unlocks the account and signs every device out.</summary>
public sealed class PasswordResetHandler(IUserStore users, ITokenStore tokens, IPasswordHasher hasher, IAccountMessenger messenger, IOptions<IdentityOptions> options, TimeProvider time)
{
    /// <summary>Always succeeds, so the form cannot be used to learn which emails have accounts.</summary>
    public async Task<Result> RequestAsync(string email, CancellationToken cancellationToken)
    {
        var user = EmailAddress.IsPlausible(email) ? await users.FindByLoginAsync(EmailAddress.Normalize(email), cancellationToken) : null;
        if (user is { Email: { } address })
        {
            var token = SecretTokens.New();
            await tokens.StoreOneTimeTokenAsync(SecretTokens.Hash(token), user.UserId, TokenPurpose.ResetPassword, time.GetUtcNow().AddMinutes(options.Value.ResetPasswordMinutes));
            await messenger.SendPasswordResetAsync(address, token, cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ResetAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (newPassword.Length is < PasswordPolicy.MinimumLength or > PasswordPolicy.MaximumLength)
        {
            return Result.Failure(Error.Validation("password_weak", PasswordPolicy.Problem(newPassword, string.Empty)!));
        }

        if (await tokens.ConsumeOneTimeTokenAsync(SecretTokens.Hash(token), TokenPurpose.ResetPassword, now) is not { } userId
            || await users.FindByIdAsync(userId, cancellationToken) is not { } user)
        {
            return Result.Failure(Error.Validation("token_invalid", "This link has expired or was already used. Request a new one."));
        }

        if (PasswordPolicy.Problem(newPassword, user.Email ?? string.Empty) is { } problem)
        {
            return Result.Failure(Error.Validation("password_weak", problem));
        }

        await users.SetPasswordAsync(userId, hasher.Hash(newPassword), now);
        await users.ClearFailedSignInsAsync(userId);
        await tokens.RevokeAllForUserAsync(userId, now);
        return Result.Success();
    }
}
