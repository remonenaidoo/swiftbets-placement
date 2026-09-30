using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Grpc.Wallet.V1;
using SwiftBets.Wallet.Application.Ledger;
using SwiftBets.Wallet.Application.Ports;
using WalletBase = SwiftBets.Contracts.Grpc.Wallet.V1.Wallet.WalletBase;

namespace SwiftBets.Wallet.Api.Grpc;

/// <summary>Internal money API for placement and payout. Every mutation is keyed; expected failures are typed replies, not status codes.</summary>
[Authorize(Policy = Roles.Service)]
public sealed class WalletGrpcService(ReserveFundsHandler reserve, SettleReservationHandler settle, TransferHandler transfer, IWalletStore store) : WalletBase
{
    public override async Task<ReservationReply> Reserve(ReserveRequest request, ServerCallContext context) =>
        WalletReplies.Reservation(await reserve.HandleAsync(Key(request.IdempotencyKey), Id(request.AccountId), Amount(request.Amount), request.Amount.Currency, request.Reference));

    public override async Task<ReservationReply> Capture(ReservationCommand request, ServerCallContext context) =>
        WalletReplies.Reservation(await settle.CaptureAsync(Key(request.IdempotencyKey), Id(request.ReservationId), context.CancellationToken));

    public override async Task<ReservationReply> Release(ReservationCommand request, ServerCallContext context) =>
        WalletReplies.Reservation(await settle.ReleaseAsync(Key(request.IdempotencyKey), Id(request.ReservationId), context.CancellationToken));

    public override async Task<PostingReply> Credit(PostingRequest request, ServerCallContext context) =>
        WalletReplies.Posting(await transfer.CreditAsync(Key(request.IdempotencyKey), Id(request.AccountId), Amount(request.Amount), request.Amount.Currency, request.Reference));

    public override async Task<PostingReply> Debit(PostingRequest request, ServerCallContext context) =>
        WalletReplies.Posting(await transfer.DebitAsync(Key(request.IdempotencyKey), Id(request.AccountId), Amount(request.Amount), request.Amount.Currency, request.Reference));

    public override async Task<BalanceReply> GetBalance(GetBalanceRequest request, ServerCallContext context) =>
        await store.GetAccountAsync(Id(request.AccountId), context.CancellationToken) is { } account
            ? new BalanceReply { Balance = WalletReplies.Balance(account) }
            : new BalanceReply { Failure = WalletReplies.Failure(Domain.WalletFailure.AccountNotFound) };

    public override async Task<ReservationReply> GetReservation(GetReservationRequest request, ServerCallContext context)
    {
        var reservation = request.ReservationId is { Length: > 0 } id
            ? await store.GetReservationAsync(Id(id), context.CancellationToken)
            : await store.FindReservationByKeyAsync(Key(request.IdempotencyKey), context.CancellationToken);
        return reservation is null
            ? new ReservationReply { Failure = WalletReplies.Failure(Domain.WalletFailure.ReservationNotFound) }
            : new ReservationReply { Reservation = WalletReplies.Map(reservation) };
    }

    private static string Key(string key) =>
        key is { Length: > 0 and <= 200 } ? key : throw new RpcException(new Status(StatusCode.InvalidArgument, "idempotency_key is required (max 200 chars)."));

    private static Guid Id(string value) =>
        Guid.TryParse(value, out var id) ? id : throw new RpcException(new Status(StatusCode.InvalidArgument, $"'{value}' is not a valid id."));

    private static long Amount(Money? money) =>
        money is { MinorUnits: > 0, Currency.Length: 3 } ? money.MinorUnits : throw new RpcException(new Status(StatusCode.InvalidArgument, "amount must be positive with an ISO currency."));
}
