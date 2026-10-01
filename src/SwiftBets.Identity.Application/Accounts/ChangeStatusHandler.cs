using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;
using SwiftBets.Identity.Application.Ports;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Application.Accounts;

/// <summary>
/// Operator changes to an account's status. Leaving Active revokes every refresh token, so no device can renew.
/// Self-exclusion is lifted only by compliance once its minimum period has run, never from here.
/// </summary>
public sealed class ChangeStatusHandler(IUserStore users, ITokenStore tokens, TimeProvider time)
{
    public async Task<Result> HandleAsync(Guid userId, AccountStatus status, string reason, string changedBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("reason_required", "Give a reason for the change."));
        }

        if (await users.FindByIdAsync(userId, cancellationToken) is not { } user)
        {
            return Result.Failure(Error.NotFound("user_not_found", "No such account."));
        }

        if (user.Status == AccountStatus.SelfExcluded && status != AccountStatus.SelfExcluded)
        {
            return Result.Failure(Error.BusinessRule("self_exclusion_locked", "A self-exclusion can only end when its period has run."));
        }

        if (user.Status == status)
        {
            return Result.Success();
        }

        var now = time.GetUtcNow();
        if (!await users.ChangeStatusAsync(userId, user.Status, status, reason.Trim(), changedBy, now))
        {
            return Result.Failure(Error.Conflict("status_changed", "The account changed while you were editing it; reload and try again."));
        }

        if (status != AccountStatus.Active)
        {
            await tokens.RevokeAllForUserAsync(userId, now);
        }

        return Result.Success();
    }
}
