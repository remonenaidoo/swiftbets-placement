using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Placement.Application.Identity;

namespace SwiftBets.Placement.Api.Endpoints;

public static class IdentityEndpoints
{
    private static readonly string[] SigningAlgorithms = ["RS256"];
    private static readonly string[] GrantTypes = ["password", "client_credentials", "refresh_token"];

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/openid-configuration", (ITokenIssuer issuer) => Results.Ok(new
        {
            issuer = issuer.Issuer,
            jwks_uri = $"{issuer.Issuer.TrimEnd('/')}/.well-known/jwks.json",
            token_endpoint = $"{issuer.Issuer.TrimEnd('/')}/auth/token",
            id_token_signing_alg_values_supported = SigningAlgorithms,
            grant_types_supported = GrantTypes,
        }));

        endpoints.MapGet("/.well-known/jwks.json", (ITokenIssuer issuer) => Results.Ok(issuer.Jwks()));

        endpoints.MapPost("/auth/token", async (TokenRequest request, TokenHandler tokens, HttpContext context, CancellationToken cancellationToken) =>
            request.GrantType switch
            {
                "password" => (await tokens.PasswordAsync(request.Username ?? string.Empty, request.Password ?? string.Empty, cancellationToken)).ToHttpResult(context),
                "client_credentials" => tokens.ClientCredentials(request.ClientId ?? string.Empty, request.ClientSecret ?? string.Empty).ToHttpResult(context),
                _ => Error.Validation("unsupported_grant_type", "grantType must be password or client_credentials.").ToHttpResult(context),
            });

        endpoints.MapPost("/auth/refresh", async (RefreshRequest request, TokenHandler tokens, HttpContext context, CancellationToken cancellationToken) =>
            (await tokens.RefreshAsync(request.RefreshToken, cancellationToken)).ToHttpResult(context));

        return endpoints;
    }

    public sealed record TokenRequest(string GrantType, string? Username, string? Password, string? ClientId, string? ClientSecret);

    public sealed record RefreshRequest(string RefreshToken);
}
