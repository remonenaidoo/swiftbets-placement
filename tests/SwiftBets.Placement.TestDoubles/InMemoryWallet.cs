using System.Collections.Concurrent;
using SwiftBets.Placement.Application.Ports;

namespace SwiftBets.Placement.TestDoubles;

/// <summary>A wallet with the real one's idempotency semantics: one reservation per reserve key, and state transitions only from Held.</summary>
public sealed class InMemoryWallet : IWalletClient
{
    private readonly ConcurrentDictionary<string, Guid> _reserveKeys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, string> _states = new();
    private readonly Lock _gate = new();

    public bool IsDown { get; set; }

    public long Balance { get; private set; } = 100_000;

    public int AppliedReserves { get; private set; }

    public string StateOf(Guid reservationId) => _states[reservationId];

    public Task<WalletCall> ReserveAsync(string idempotencyKey, Guid accountId, long amount, string currency, string reference)
    {
        if (IsDown)
        {
            return Task.FromResult(WalletCall.Unavailable);
        }

        lock (_gate)
        {
            if (_reserveKeys.TryGetValue(idempotencyKey, out var existing))
            {
                return Task.FromResult(new WalletCall(WalletCallStatus.Succeeded, existing, _states[existing]));
            }

            if (Balance < amount)
            {
                return Task.FromResult(new WalletCall(WalletCallStatus.Refused, FailureCode: "insufficient_funds"));
            }

            var id = Guid.NewGuid();
            Balance -= amount;
            AppliedReserves++;
            _reserveKeys[idempotencyKey] = id;
            _states[id] = "Held";
            return Task.FromResult(new WalletCall(WalletCallStatus.Succeeded, id, "Held"));
        }
    }

    public Task<WalletCall> CaptureAsync(string idempotencyKey, Guid reservationId) => Move(reservationId, "Captured", refund: false);

    public Task<WalletCall> ReleaseAsync(string idempotencyKey, Guid reservationId) => Move(reservationId, "Released", refund: true);

    public Task<WalletCall> FindReservationAsync(string reserveIdempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(IsDown ? WalletCall.Unavailable
            : _reserveKeys.TryGetValue(reserveIdempotencyKey, out var id) ? new WalletCall(WalletCallStatus.Succeeded, id, _states[id])
            : new WalletCall(WalletCallStatus.Succeeded, ReservationState: "None"));

    public Task<WalletCall> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(new WalletCall(WalletCallStatus.Succeeded, Available: Balance));

    private Task<WalletCall> Move(Guid reservationId, string target, bool refund)
    {
        if (IsDown)
        {
            return Task.FromResult(WalletCall.Unavailable);
        }

        lock (_gate)
        {
            var state = _states[reservationId];
            if (state == target)
            {
                return Task.FromResult(new WalletCall(WalletCallStatus.Succeeded, reservationId, state));
            }

            if (state != "Held")
            {
                return Task.FromResult(new WalletCall(WalletCallStatus.Refused, FailureCode: "invalid_reservation_state"));
            }

            _states[reservationId] = target;
            if (refund)
            {
                Balance += 2_500;
            }

            return Task.FromResult(new WalletCall(WalletCallStatus.Succeeded, reservationId, target));
        }
    }
}
