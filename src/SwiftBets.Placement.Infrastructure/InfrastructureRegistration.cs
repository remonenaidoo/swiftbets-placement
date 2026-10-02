using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Redis;
using SwiftBets.BuildingBlocks.Resilience;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Placement.Application.Identity;
using SwiftBets.Placement.Application.Placing;
using SwiftBets.Placement.Application.Ports;
using SwiftBets.Contracts.Config;
using SwiftBets.Contracts.Risk;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Placement.Infrastructure.Config;
using SwiftBets.Placement.Infrastructure.Identity;
using SwiftBets.Placement.Infrastructure.Offer;
using SwiftBets.Placement.Infrastructure.Persistence;
using SwiftBets.Placement.Infrastructure.Wallet;
using SwiftBets.Placement.Infrastructure.Workers;
using WalletGrpc = SwiftBets.Contracts.Grpc.Wallet.V1.Wallet;

namespace SwiftBets.Placement.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddPlacementInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSqlServerPersistence(Required(configuration, "ConnectionStrings:SbPlacement"));
        services.AddKafkaMessaging(configuration);
        services.AddSqlServerOutbox(configuration);
        services.AddCompactedState<ConfigEntryV1>(Topics.ConfigEntries);
        services.AddCompactedState<ExposureLimitV1>(Topics.ExposureLimits);
        services.AddSingleton<IExposureLimits, CompactedExposureLimits>();
        services.AddSingleton<IPlacementSettings, ConfigPlacementSettings>();
        services.AddSwiftBetsRedis(Required(configuration, "ConnectionStrings:Redis"));
        services.AddFaultInjection(configuration);
        services.AddValidatedOptions<PlacementOptions>(configuration, PlacementOptions.SectionName);
        services.AddValidatedOptions<Application.Identity.IdentityOptions>(configuration, Application.Identity.IdentityOptions.SectionName);
        services.AddValidatedOptions<WalletClientOptions>(configuration, WalletClientOptions.SectionName);

        services.AddSingleton<ICouponStore, SqlCouponStore>();
        services.AddSingleton<ILiabilityLedger, RedisLiabilityLedger>();
        services.AddSingleton<IOfferReader, RedisOfferReader>();
        services.AddSingleton<IIdentityStore, SqlIdentityStore>();
        services.AddSingleton<ITokenIssuer, RsaTokenIssuer>();
        services.AddSingleton<Application.Identity.IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<ServiceTokenCache>();
        services.AddScoped<IWalletClient, GrpcWalletClient>();
        services.AddGrpcClient<WalletGrpc.WalletClient>((sp, grpc) => grpc.Address = new Uri(sp.GetRequiredService<IOptions<WalletClientOptions>>().Value.GrpcAddress))
            .ConfigureChannel(channel =>
            {
                channel.ServiceConfig = GrpcResilience.KeyedServiceConfig;
                channel.UnsafeUseInsecureChannelCallCredentials = true;
            })
            .AddCallCredentials(async (context, metadata, sp) =>
            {
                // Identity issues the wallet token when placement has client credentials; the local token is for tests.
                var token = sp.GetService<ClientCredentialsTokenProvider>() is { } provider
                    ? await provider.GetTokenAsync(context.CancellationToken)
                    : sp.GetRequiredService<ServiceTokenCache>().Token;
                metadata.Add("Authorization", $"Bearer {token}");
            })
            .AddKeyedGrpcResilience();

        if (configuration.GetSection(ClientCredentialsOptions.SectionName).Exists())
        {
            services.AddClientCredentials(configuration);
        }

        services.AddHostedService<DemoUserSeeder>();
        services.AddHostedService<SagaSweeperWorker>();
        return services;
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"Configuration '{key}' is required.");
}
