using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SwiftBets.Identity.Application.Ports;

namespace SwiftBets.Identity.Infrastructure.Messaging;

/// <summary>
/// Sends account emails over SMTP (Mailpit in dev and CI, a provider's relay in staging). With no SMTP host configured
/// nothing is sent; in Development the link is logged so a developer can follow it.
/// </summary>
public sealed partial class SmtpAccountMessenger(IOptions<AccountEmailOptions> options, IHostEnvironment environment, ILogger<SmtpAccountMessenger> logger) : IAccountMessenger
{
    public Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken) =>
        SendAsync(email, AccountEmails.Verification(Link("account/verify", token)), cancellationToken);

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken) =>
        SendAsync(email, AccountEmails.PasswordReset(Link("account/reset-password", token)), cancellationToken);

    public Task SendAlreadyRegisteredAsync(string email, CancellationToken cancellationToken) =>
        SendAsync(email, AccountEmails.AlreadyRegistered(Link("account/sign-in", null), Link("account/forgot-password", null)), cancellationToken);

    private string Link(string path, string? token) =>
        $"{options.Value.PublicBaseUrl.TrimEnd('/')}/{path}" + (token is null ? string.Empty : $"?token={Uri.EscapeDataString(token)}");

    private async Task SendAsync(string to, (string Subject, string Body) email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            if (environment.IsDevelopment())
            {
                LogNotSent(email.Subject, to, email.Body);
            }

            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = email.Subject;
        message.Body = new TextPart("plain") { Text = email.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "No SMTP host configured; not sending \"{Subject}\" to {To}:\n{Body}")]
    private partial void LogNotSent(string subject, string to, string body);
}
