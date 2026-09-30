using FluentValidation;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Wallet.Application.Ledger;
using SwiftBets.Wallet.Application.Ports;

namespace SwiftBets.Wallet.Api.Endpoints;

public static class TopUpEndpoint
{
    public static IEndpointRouteBuilder MapTopUp(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/accounts/{accountId:guid}/topup", async (Guid accountId, TopUpRequest request, HttpContext context, TransferHandler transfer) =>
        {
            if (context.Request.Headers["Idempotency-Key"].ToString() is not { Length: > 0 and <= 200 } key)
            {
                return Error.Validation("idempotency_key_required", "Send an Idempotency-Key header.").ToHttpResult(context);
            }

            var outcome = await transfer.TopUpAsync($"topup_{key}", accountId, request.MinorUnits, request.Currency, $"operator top-up by {context.User.FindFirst("sub")?.Value}");
            return outcome.Failure is { } failure
                ? Error.BusinessRule(failure.ToString(), $"Top-up refused: {failure}.").ToHttpResult(context)
                : Results.Ok(new { outcome.WasApplied, outcome.Account!.Available, outcome.Account.Reserved });
        })
        .AddEndpointFilter<ValidationFilter<TopUpRequest>>()
        .RequireAuthorization(Roles.Operator);

        endpoints.MapPut("/accounts/{accountId:guid}/blacklist", (Guid accountId, HttpContext context, IWalletStore store) => SetAsync(accountId, true, context, store))
            .RequireAuthorization(Roles.Operator);
        endpoints.MapDelete("/accounts/{accountId:guid}/blacklist", (Guid accountId, HttpContext context, IWalletStore store) => SetAsync(accountId, false, context, store))
            .RequireAuthorization(Roles.Operator);
        return endpoints;
    }

    private static async Task<IResult> SetAsync(Guid accountId, bool blacklisted, HttpContext context, IWalletStore store) =>
        await store.SetBlacklistedAsync(accountId, blacklisted, context.RequestAborted)
            ? Results.Ok(new { accountId, blacklisted })
            : Error.NotFound("account_not_found", "No punter account with that id.").ToHttpResult(context);

    public sealed record TopUpRequest(long MinorUnits, string Currency);

    public sealed class TopUpRequestValidator : AbstractValidator<TopUpRequest>
    {
        public TopUpRequestValidator()
        {
            RuleFor(r => r.MinorUnits).InclusiveBetween(1, 10_000_000);
            RuleFor(r => r.Currency).Length(3);
        }
    }
}
