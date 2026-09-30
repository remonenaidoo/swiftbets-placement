using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Application.Ports;

public interface IWalletStore
{
    Task<IWalletTransaction> BeginAsync();

    Task<Account?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken);

    Task<Reservation?> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<Reservation?> FindReservationByKeyAsync(string reserveIdempotencyKey, CancellationToken cancellationToken);

    /// <summary>A blacklisted punter keeps their balance but receives no credits; payouts to them are dead-lettered.</summary>
    Task<bool> SetBlacklistedAsync(Guid accountId, bool isBlacklisted, CancellationToken cancellationToken);
}
