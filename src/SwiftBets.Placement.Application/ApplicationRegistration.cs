using Microsoft.Extensions.DependencyInjection;
using SwiftBets.Placement.Application.Identity;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Application.Sweeping;

namespace SwiftBets.Placement.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddPlacementApplication(this IServiceCollection services)
    {
        services.AddScoped<PlaceCouponHandler>();
        services.AddScoped<RefreshCouponHandler>();
        services.AddScoped<SweepOrphansHandler>();
        services.AddScoped<TokenHandler>();
        return services;
    }
}
