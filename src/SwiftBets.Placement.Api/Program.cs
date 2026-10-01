using FluentValidation;
using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Placement.Api.Endpoints;
using SwiftBets.Placement.Application;
using SwiftBets.Placement.Infrastructure;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-placement");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddSwiftBetsJwtBearer(builder.Configuration);
builder.Services.AddScoped<IValidator<CouponEndpoints.PlaceCouponRequest>, CouponEndpoints.PlaceCouponRequestValidator>();
builder.Services.AddPlacementApplication();
builder.Services.AddPlacementInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.UseAuthentication();
app.UseAuthorization();
app.MapSwiftBetsOperationalEndpoints();
app.MapIdentityEndpoints();
app.MapCouponEndpoints();

await app.RunAsync();
return 0;

public partial class Program;
