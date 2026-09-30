using Grpc.Core;
using Microsoft.Extensions.Options;
using SwiftBets.Contracts.Grpc.Wallet.V1;
using SwiftBets.Placement.Application.Ports;
using WalletGrpc = SwiftBets.Contracts.Grpc.Wallet.V1.Wallet;

namespace SwiftBets.Placement.Infrastructure.Wallet;

/// <summary>
/// Adapts the wallet's gRPC API. Any transport failure (unreachable, deadline, open breaker) is reported as
/// <see cref="WalletCallStatus.Unavailable"/>: the outcome is unknown, and the saga's keys make reconciling it safe.
/// </summary>
public sealed class GrpcWalletClient(WalletGrpc.WalletClient client, IOptions<WalletClientOptions> options) : IWalletClient
{
    public Task<WalletCall> ReserveAsync(string idempotencyKey, Guid accountId, long amount, string currency, string reference) =>
        CallAsync(async deadline => Map(await client.ReserveAsync(new ReserveRequest
        {
            IdempotencyKey = idempotencyKey, AccountId = accountId.ToString(), Amount = new Money { MinorUnits = amount, Currency = currency }, Reference = reference,
        }, deadline: deadline)));

    public Task<WalletCall> CaptureAsync(string idempotencyKey, Guid reservationId) =>
        CallAsync(async deadline => Map(await client.CaptureAsync(new ReservationCommand { IdempotencyKey = idempotencyKey, ReservationId = reservationId.ToString() }, deadline: deadline)));

    public Task<WalletCall> ReleaseAsync(string idempotencyKey, Guid reservationId) =>
        CallAsync(async deadline => Map(await client.ReleaseAsync(new ReservationCommand { IdempotencyKey = idempotencyKey, ReservationId = reservationId.ToString() }, deadline: deadline)));

    public Task<WalletCall> FindReservationAsync(string reserveIdempotencyKey, CancellationToken cancellationToken) =>
        CallAsync(async deadline => Map(await client.GetReservationAsync(new GetReservationRequest { IdempotencyKey = reserveIdempotencyKey }, deadline: deadline, cancellationToken: cancellationToken)));

    public Task<WalletCall> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken) =>
        CallAsync(async deadline =>
        {
            var reply = await client.GetBalanceAsync(new GetBalanceRequest { AccountId = accountId.ToString() }, deadline: deadline, cancellationToken: cancellationToken);
            return reply.OutcomeCase == BalanceReply.OutcomeOneofCase.Balance
                ? new WalletCall(WalletCallStatus.Succeeded, Available: reply.Balance.Available.MinorUnits)
                : new WalletCall(WalletCallStatus.Refused, FailureCode: Code(reply.Failure));
        });

    private async Task<WalletCall> CallAsync(Func<DateTime, Task<WalletCall>> call)
    {
        try
        {
            return await call(DateTime.UtcNow.AddSeconds(options.Value.DeadlineSeconds));
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.Internal or StatusCode.Unknown or StatusCode.ResourceExhausted or StatusCode.Cancelled)
        {
            return WalletCall.Unavailable;
        }
        catch (HttpRequestException)
        {
            return WalletCall.Unavailable;
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            return WalletCall.Unavailable;
        }
        catch (TimeoutException)
        {
            return WalletCall.Unavailable;
        }
    }

    private static WalletCall Map(ReservationReply reply) =>
        reply.OutcomeCase == ReservationReply.OutcomeOneofCase.Reservation
            ? new WalletCall(WalletCallStatus.Succeeded, Guid.Parse(reply.Reservation.ReservationId), reply.Reservation.State switch
            {
                ReservationState.Held => "Held",
                ReservationState.Captured => "Captured",
                _ => "Released",
            })
            : reply.Failure.Code == WalletFailureCode.ReservationNotFound
                ? new WalletCall(WalletCallStatus.Succeeded, ReservationState: "None")
                : new WalletCall(WalletCallStatus.Refused, FailureCode: Code(reply.Failure));

    private static string Code(WalletFailure failure) => failure.Code switch
    {
        WalletFailureCode.InsufficientFunds => "insufficient_funds",
        WalletFailureCode.AccountNotFound => "wallet_account_not_found",
        WalletFailureCode.AccountBlacklisted => "wallet_account_blacklisted",
        WalletFailureCode.CurrencyMismatch => "currency_mismatch",
        WalletFailureCode.IdempotencyConflict => "idempotency_conflict",
        WalletFailureCode.InvalidState => "invalid_reservation_state",
        _ => "wallet_refused",
    };
}
