using Microsoft.Extensions.Options;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;
using SwiftBets.Identity.Application.Ports;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Application.Accounts;

public sealed class EmailVerificationHandler(IUserStore users, ITokenStore tokens, IAccountMessenger messenger, IOptions<IdentityOptions> options, TimeProvider time)
{
    public async Task<Result> VerifyAsync(string token)
    {
        var now = time.GetUtcNow();
        if (await tokens.ConsumeOneTimeTokenAsync(SecretTokens.Hash(token), TokenPurpose.VerifyEmail, now) is not { } userId)
        {
            return Result.Failure(Error.Validation("token_invalid", "This link has expired or was already used. Request a new one."));
        }

        await users.MarkEmailVerifiedAsync(userId, now);
        return Result.Success();
    }

    /// <summary>Always succeeds; an unknown or already verified address simply gets no email.</summary>
    public async Task<Result> ResendAsync(string email, CancellationToken cancellationToken)
    {
        var user = EmailAddress.IsPlausible(email) ? await users.FindByLoginAsync(EmailAddress.Normalize(email), cancellationToken) : null;
        if (user is { Email: { } address, EmailVerifiedAt: null })
        {
            var token = SecretTokens.New();
            await tokens.StoreOneTimeTokenAsync(SecretTokens.Hash(token), user.UserId, TokenPurpose.VerifyEmail, time.GetUtcNow().AddHours(options.Value.VerifyEmailHours));
            await messenger.SendEmailVerificationAsync(address, token, cancellationToken);
        }

        return Result.Success();
    }
}
