using FluentValidation;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Placement;
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
                [.. request.Legs.Select(l => new LegSelection(l.FixtureId, l.MarketId, l.SelectionId, l.Odds, l.OfferVersion, l.Banker))],
                request.Bets?.Select(ToBet).ToList());
            var result = await handler.HandleAsync(command, cancellationToken);
            if (result.Replayed)
            {
                context.Response.Headers["Idempotent-Replay"] = "true";
            }

            return Results.Content(result.Body, result.Status >= 400 ? "application/problem+json" : "application/json", statusCode: result.Status);
        })
        .AddEndpointFilter<ValidationFilter<PlaceCouponRequest>>()
        .RequireAuthorization(Roles.Punter);

        endpoints.MapPost("/coupons/refresh", async (RefreshCouponRequest request, HttpContext context, RefreshCouponHandler handler, CancellationToken cancellationToken) =>
            request.Legs is not { Count: > 0 and <= 20 }
                ? Error.Validation("invalid_leg_count", "Refresh 1 to 20 legs.").ToHttpResult(context)
                : Results.Ok(await handler.HandleAsync([.. request.Legs.Select(l => new LegSelection(l.FixtureId, l.MarketId, l.SelectionId, l.Odds, 0))], cancellationToken)))
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

    /// <summary>A named bet (trixie, yankee, ...) needs no folds; otherwise folds are the line sizes over the non-banker legs.</summary>
    private static BetRequest ToBet(PlaceCouponBet bet) =>
        bet.Folds is { Count: > 0 } folds
            ? new BetRequest(bet.Name ?? "system", folds, bet.UnitStake)
            : new BetRequest(bet.Name!, SystemBets.Named.TryGetValue(bet.Name ?? string.Empty, out var named) ? named.Folds : [], bet.UnitStake);

    public sealed record PlaceCouponLeg(string FixtureId, string MarketId, string SelectionId, decimal Odds, long OfferVersion, bool Banker = false);

    public sealed record PlaceCouponBet(string? Name, IReadOnlyList<int>? Folds, long UnitStake);

    /// <summary>Stake is the coupon's total. Without bets the coupon is one single or accumulator over all its legs.</summary>
    public sealed record PlaceCouponRequest(long Stake, string Currency, IReadOnlyList<PlaceCouponLeg> Legs, IReadOnlyList<PlaceCouponBet>? Bets = null);

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
            RuleFor(r => r.Bets!.Count).InclusiveBetween(1, 10).When(r => r.Bets is not null).WithErrorCode("invalid_bets");
            RuleForEach(r => r.Bets).ChildRules(bet =>
            {
                bet.RuleFor(b => b.UnitStake).GreaterThan(0);
                bet.RuleFor(b => b.Name).MaximumLength(40);
                bet.RuleFor(b => b.Name).Must(n => n is not null && SystemBets.Named.ContainsKey(n)).When(b => b.Folds is not { Count: > 0 })
                    .WithErrorCode("invalid_bets").WithMessage("Name a known bet (trixie, yankee, ...) or give its folds.");
                bet.RuleFor(b => b.Folds!.Count).LessThanOrEqualTo(20).When(b => b.Folds is not null);
            });
        }
    }

    public sealed record RefreshCouponRequest(IReadOnlyList<RefreshLeg> Legs);

    public sealed record RefreshLeg(string FixtureId, string MarketId, string SelectionId, decimal Odds);
}
