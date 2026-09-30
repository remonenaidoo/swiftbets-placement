using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Wallet.Application;
using SwiftBets.Wallet.Infrastructure;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-wallet");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddWalletApplication();
builder.Services.AddWalletInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.MapSwiftBetsOperationalEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "swiftbets-wallet" })).ExcludeFromDescription();

await app.RunAsync();
return 0;

public partial class Program;
