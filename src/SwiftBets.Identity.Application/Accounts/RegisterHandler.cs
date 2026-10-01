using Microsoft.Extensions.Options;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;
using SwiftBets.Identity.Application.Ports;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Application.Accounts;

/// <summary>
/// Registers a customer after the age gate. The answer is the same whether or not the email already has an account:
/// the existing owner gets an "already registered" email instead, so the form cannot be used to probe for accounts.
/// </summary>
public sealed class RegisterHandler(IUserStore users, ITokenStore tokens, IPasswordHasher hasher, IAccountMessenger messenger, IOptions<IdentityOptions> options, TimeProvider time)
{
    public async Task<Result> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (!EmailAddress.IsPlausible(command.Email))
        {
            return Result.Failure(Error.Validation("email_invalid", "Enter a valid email address."));
        }

        if (PasswordPolicy.Problem(command.Password, command.Email) is { } problem)
        {
            return Result.Failure(Error.Validation("password_weak", problem));
        }

        if (!AgePolicy.IsOldEnough(command.DateOfBirth, DateOnly.FromDateTime(now.UtcDateTime)))
        {
            return Result.Failure(Error.BusinessRule("underage", $"You must be {AgePolicy.MinimumAge} or older to open an account."));
        }

        if (!options.Value.Countries.Contains(command.Country, StringComparer.Ordinal))
        {
            return Result.Failure(Error.BusinessRule("country_not_supported", "Accounts cannot be opened from this country."));
        }

        if (!options.Value.Currencies.Contains(command.Currency, StringComparer.Ordinal))
        {
            return Result.Failure(Error.BusinessRule("currency_not_supported", "This currency is not offered."));
        }

        var email = EmailAddress.Normalize(command.Email);
        var user = new User(Guid.CreateVersion7(), email, null, hasher.Hash(command.Password), command.DateOfBirth, options.Value.Brand,
            command.Country, command.Currency, AccountStatus.Active, null, 0, null, null, [RoleNames.Customer]);
        if (!await users.CreateAsync(user, now))
        {
            await messenger.SendAlreadyRegisteredAsync(email, cancellationToken);
            return Result.Success();
        }

        var token = SecretTokens.New();
        await tokens.StoreOneTimeTokenAsync(SecretTokens.Hash(token), user.UserId, TokenPurpose.VerifyEmail, now.AddHours(options.Value.VerifyEmailHours));
        await messenger.SendEmailVerificationAsync(email, token, cancellationToken);
        return Result.Success();
    }
}
