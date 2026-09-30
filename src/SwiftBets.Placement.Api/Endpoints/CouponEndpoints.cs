using FluentValidation;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Api.Endpoints;

public static class CouponEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapCouponEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/coupons", async (PlaceCouponRequest request, HttpContext context, PlaceCouponHandler handler, CancellationToken cancellationToken) =>
        {
            if (context.Request.Headers[IdempotencyHeader].ToString() is not { Length: >= 8 and <= 200 } key)
            {
                return Error.Validation("idempotency_key_required", $"Send an {IdempotencyHeader} header of 8 to 200 characters.").ToHttpResult(context);
            }

            var command = new PlaceCouponCommand(
                PunterId(context),
                key,
                request.Stake,
                request.Currency,
                [.. request.Legs.Select(l => new LegSelection(l.FixtureId, l.MarketId, l.SelectionId, l.Odds, l.OfferVersion))]);
            var result = await handler.HandleAsync(command, cancellationToken);
            if (result.Replayed)
            {
                context.Response.Headers["Idempotent-Replay"] = "true";
            }

            return Results.Content(result.Body, result.Status >= 400 ? "application/problem+json" : "application/json", statusCode: result.Status);
        })
        .AddEndpointFilter<ValidationFilter<PlaceCouponRequest>>()
        .RequireAuthorization(Roles.Punter);

        endpoints.MapGet("/coupons/{couponId:guid}", async (Guid couponId, HttpContext context, ICouponStore store, CancellationToken cancellationToken) =>
        {
            var coupon = await store.GetCouponAsync(couponId, cancellationToken);
            return coupon is null || (coupon.PunterId != PunterId(context) && !context.User.IsInRole(Roles.Operator))
                ? Error.NotFound("coupon_not_found", "No such coupon.").ToHttpResult(context)
                : Results.Content(PlacementResponses.Coupon(coupon), "application/json");
        })
        .RequireAuthorization();

        endpoints.MapGet("/me/balance", async (HttpContext context, IWalletClient wallet, CancellationToken cancellationToken) =>
        {
            var balance = await wallet.GetBalanceAsync(PunterId(context), cancellationToken);
            return balance.Status switch
            {
                WalletCallStatus.Succeeded => Results.Ok(new { available = new { minorUnits = balance.Available, currency = "ZAR" } }),
                WalletCallStatus.Unavailable => Error.Unavailable("wallet_unavailable", "The wallet is unavailable.").ToHttpResult(context),
                _ => Error.NotFound(balance.FailureCode ?? "wallet_account_not_found", "No wallet for this user.").ToHttpResult(context),
            };
        })
        .RequireAuthorization(Roles.Punter);

        return endpoints;
    }

    private static Guid PunterId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id) ? id : throw new BadHttpRequestException("Token subject is not a user id.", 401);

    public sealed record PlaceCouponLeg(string FixtureId, string MarketId, string SelectionId, decimal Odds, long OfferVersion);

    public sealed record PlaceCouponRequest(long Stake, string Currency, IReadOnlyList<PlaceCouponLeg> Legs);

    public sealed class PlaceCouponRequestValidator : AbstractValidator<PlaceCouponRequest>
    {
        public PlaceCouponRequestValidator()
        {
            RuleFor(r => r.Stake).GreaterThan(0);
            RuleFor(r => r.Currency).Equal("ZAR").WithErrorCode("currency_not_supported");
            RuleFor(r => r.Legs).NotEmpty();
            RuleFor(r => r.Legs.Count).LessThanOrEqualTo(20).When(r => r.Legs is not null);
            RuleForEach(r => r.Legs).ChildRules(leg =>
            {
                leg.RuleFor(l => l.FixtureId).NotEmpty().MaximumLength(100);
                leg.RuleFor(l => l.MarketId).NotEmpty().MaximumLength(120);
                leg.RuleFor(l => l.SelectionId).NotEmpty().MaximumLength(50);
                leg.RuleFor(l => l.Odds).GreaterThanOrEqualTo(1.01m).LessThanOrEqualTo(1000m);
                leg.RuleFor(l => l.OfferVersion).GreaterThan(0);
            });
        }
    }
}
