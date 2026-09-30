using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Placement.Application;
using SwiftBets.Placement.Infrastructure;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-placement");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddPlacementApplication();
builder.Services.AddPlacementInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.MapSwiftBetsOperationalEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "swiftbets-placement" })).ExcludeFromDescription();

await app.RunAsync();
return 0;

public partial class Program;
