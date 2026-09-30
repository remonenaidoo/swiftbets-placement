using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ports;

public interface IWalletStore
{
    Task<IWalletTransaction> BeginAsync();

    Task<Account?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken);

    Task<Reservation?> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<Reservation?> FindReservationByKeyAsync(string reserveIdempotencyKey, CancellationToken cancellationToken);
}
