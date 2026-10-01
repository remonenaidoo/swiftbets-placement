using Microsoft.Extensions.DependencyInjection;
using SwiftBets.Identity.Application.Accounts;
using SwiftBets.Identity.Application.Tokens;

namespace SwiftBets.Identity.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterHandler>();
        services.AddScoped<EmailVerificationHandler>();
        services.AddScoped<PasswordResetHandler>();
        services.AddScoped<ChangeStatusHandler>();
        services.AddScoped<TokenHandler>();
        return services;
    }
}
