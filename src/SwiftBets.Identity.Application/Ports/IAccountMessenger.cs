namespace SwiftBets.Identity.Application.Ports;

/// <summary>Account emails. The token is the raw single-use value; only its hash is stored.</summary>
public interface IAccountMessenger
{
    Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken);

    /// <summary>Sent instead of a second account, so registering cannot be used to learn which emails have accounts.</summary>
    Task SendAlreadyRegisteredAsync(string email, CancellationToken cancellationToken);
}
