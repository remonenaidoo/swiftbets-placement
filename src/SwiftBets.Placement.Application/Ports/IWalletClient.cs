namespace SwiftBets.Placement.Application.Ports;

public interface IWalletClient
{
    Task<WalletCall> ReserveAsync(string idempotencyKey, Guid accountId, long amount, string currency, string reference);

    Task<WalletCall> CaptureAsync(string idempotencyKey, Guid reservationId);

    Task<WalletCall> ReleaseAsync(string idempotencyKey, Guid reservationId);

    Task<WalletCall> FindReservationAsync(string reserveIdempotencyKey, CancellationToken cancellationToken);

    Task<WalletCall> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken);
}
