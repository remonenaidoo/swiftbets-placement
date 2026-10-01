using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Identity.Api.Endpoints;
using SwiftBets.Identity.Application;
using SwiftBets.Identity.Domain;
using SwiftBets.Identity.Infrastructure;
using SwiftBets.Identity.Infrastructure.Security;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-identity");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddSwiftBetsJwtBearer(builder.Configuration);

// Identity validates its own tokens with the key it signs them with, instead of fetching its own JWKS over HTTP.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).PostConfigure<RsaTokenIssuer>((options, issuer) =>
{
    var configuration = new OpenIdConnectConfiguration { Issuer = issuer.Issuer };
    configuration.SigningKeys.Add(issuer.SigningKey);
    options.Configuration = configuration;
    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    options.TokenValidationParameters.ValidIssuer = issuer.Issuer;
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Permissions.UsersRead, p => p.RequireClaim("perm", Permissions.UsersRead))
    .AddPolicy(Permissions.UsersStatusWrite, p => p.RequireClaim("perm", Permissions.UsersStatusWrite));
builder.Services.AddScoped<IValidator<AccountEndpoints.RegisterRequest>, AccountEndpoints.RegisterRequestValidator>();
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.UseAuthentication();
app.UseAuthorization();
app.MapSwiftBetsOperationalEndpoints();
app.MapTokenEndpoints();
app.MapAccountEndpoints();

await app.RunAsync();
return 0;

public partial class Program;
