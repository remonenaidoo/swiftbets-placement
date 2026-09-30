using Grpc.Core;
using Grpc.Core.Interceptors;
using SwiftBets.BuildingBlocks.Core;

namespace SwiftBets.Wallet.Api.Grpc;

/// <summary>An armed <c>wallet.unavailable</c> fault turns wallet calls into Unavailable, simulating an outage without stopping the process.</summary>
public sealed class WalletFaultInterceptor(IFaultPoint faults) : Interceptor
{
    public const string FaultUnavailable = "wallet.unavailable";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await faults.HitAsync(FaultUnavailable, context.CancellationToken);
        }
        catch (FaultInjectedException)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "wallet unavailable (fault injected)"));
        }

        return await continuation(request, context);
    }
}
