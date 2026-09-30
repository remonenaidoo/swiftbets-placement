using Microsoft.Extensions.DependencyInjection;

namespace SwiftBets.Wallet.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddWalletApplication(this IServiceCollection services) => services;
}
