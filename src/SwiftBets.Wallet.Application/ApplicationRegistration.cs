using Microsoft.Extensions.DependencyInjection;
using SwiftBets.Wallet.Application.Ledger;

namespace SwiftBets.Wallet.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddWalletApplication(this IServiceCollection services)
    {
        services.AddScoped<PostingRunner>();
        services.AddScoped<ReserveFundsHandler>();
        services.AddScoped<SettleReservationHandler>();
        services.AddScoped<TransferHandler>();
        return services;
    }
}
