using Microsoft.Extensions.DependencyInjection;
using SwiftBets.Wallet.Application.Ledger;
using SwiftBets.Wallet.Application.Reconciliation;

namespace SwiftBets.Wallet.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddWalletApplication(this IServiceCollection services)
    {
        services.AddScoped<PostingRunner>();
        services.AddScoped<ReserveFundsHandler>();
        services.AddScoped<SettleReservationHandler>();
        services.AddScoped<TransferHandler>();
        services.AddScoped<ReconcileLedgerHandler>();
        return services;
    }
}
