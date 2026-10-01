using FluentValidation;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Identity.Application.Accounts;
using SwiftBets.Identity.Application.Ports;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Api.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth");

        auth.MapPost("/register", async (RegisterRequest request, RegisterHandler handler, HttpContext context, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new RegisterCommand(request.Email, request.Password, request.DateOfBirth, request.Country, request.Currency), cancellationToken);
            return result.IsSuccess
                ? Results.Accepted(value: new { message = "Check your email to confirm your address." })
                : result.Error!.ToHttpResult(context);
        })
        .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        auth.MapPost("/verify-email", async (TokenBody body, EmailVerificationHandler handler, HttpContext context) =>
            (await handler.VerifyAsync(body.Token)).ToHttpResult(context));

        auth.MapPost("/verify-email/resend", async (EmailBody body, EmailVerificationHandler handler, CancellationToken cancellationToken) =>
        {
            await handler.ResendAsync(body.Email, cancellationToken);
            return Results.Accepted();
        });

        auth.MapPost("/password-reset", async (EmailBody body, PasswordResetHandler handler, CancellationToken cancellationToken) =>
        {
            await handler.RequestAsync(body.Email, cancellationToken);
            return Results.Accepted();
        });

        auth.MapPost("/password-reset/confirm", async (ResetBody body, PasswordResetHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            (await handler.ResetAsync(body.Token, body.Password, cancellationToken)).ToHttpResult(context));

        endpoints.MapGet("/profile", async (HttpContext context, IUserStore users, CancellationToken cancellationToken) =>
            Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId) && await users.FindByIdAsync(userId, cancellationToken) is { } user
                ? Results.Ok(Profile.From(user))
                : Error.NotFound("user_not_found", "No such account.").ToHttpResult(context))
            .RequireAuthorization();

        var admin = endpoints.MapGroup("/admin/users");

        admin.MapGet("/{userId:guid}", async (Guid userId, IUserStore users, HttpContext context, CancellationToken cancellationToken) =>
            await users.FindByIdAsync(userId, cancellationToken) is { } user
                ? Results.Ok(Profile.From(user))
                : Error.NotFound("user_not_found", "No such account.").ToHttpResult(context))
            .RequireAuthorization(Permissions.UsersRead);

        admin.MapPut("/{userId:guid}/status", async (Guid userId, StatusBody body, ChangeStatusHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            Enum.TryParse<AccountStatus>(body.Status, ignoreCase: true, out var status) && Enum.IsDefined(status)
                ? (await handler.HandleAsync(userId, status, body.Reason, context.User.FindFirst("sub")?.Value ?? "unknown", cancellationToken)).ToHttpResult(context)
                : Error.Validation("status_invalid", "Status is one of active, suspended, closed, selfExcluded.").ToHttpResult(context))
            .RequireAuthorization(Permissions.UsersStatusWrite);

        return endpoints;
    }

    public sealed record RegisterRequest(string Email, string Password, DateOnly DateOfBirth, string Country, string Currency);

    public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            RuleFor(r => r.Email).NotEmpty().MaximumLength(EmailAddress.MaxLength);
            RuleFor(r => r.Password).NotEmpty().MaximumLength(PasswordPolicy.MaximumLength);
            RuleFor(r => r.Country).Length(2);
            RuleFor(r => r.Currency).Length(3);
        }
    }

    public sealed record TokenBody(string Token);

    public sealed record EmailBody(string Email);

    public sealed record ResetBody(string Token, string Password);

    public sealed record StatusBody(string Status, string Reason);

    /// <summary>What an account looks like to its owner and to staff. Never includes the password hash.</summary>
    public sealed record Profile(Guid UserId, string? Email, string? Username, bool EmailVerified, string Status, string Brand, string Country, string Currency, IReadOnlyList<string> Roles)
    {
        public static Profile From(User user) => new(
            user.UserId, user.Email, user.Username, user.EmailVerifiedAt is not null, user.Status.ToString(), user.Brand, user.Country, user.Currency,
            [.. user.Roles.Select(RoleNames.Canonical)]);
    }
}
