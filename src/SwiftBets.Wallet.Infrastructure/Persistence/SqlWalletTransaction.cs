using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Wallet.Application.Ports;
using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Infrastructure.Persistence;

internal sealed class SqlWalletTransaction(SqlConnection connection, SqlTransaction transaction, TimeProvider time) : IWalletTransaction
{
    private static readonly SqlResources Sql = SqlResources.For<SqlWalletTransaction>();

    public async Task<StoredPosting?> FindPostingAsync(string idempotencyKey)
    {
        var row = await connection.QuerySingleOrDefaultAsync<PostingRow>(Sql.Get("Wallet.FindPosting"), new { IdempotencyKey = idempotencyKey }, transaction);
        return row is null ? null : new StoredPosting(row.PostingId, (PostingKind)row.Kind, row.AccountId, row.Amount, row.ReservationId);
    }

    public async Task<Account?> LockAccountAsync(Guid accountId) =>
        (await connection.QuerySingleOrDefaultAsync<AccountRow>(Sql.Get("Wallet.LockAccount"), new { AccountId = accountId }, transaction))?.ToDomain();

    public async Task<Reservation?> LockReservationAsync(Guid reservationId) =>
        (await connection.QuerySingleOrDefaultAsync<ReservationRow>(Sql.Get("Wallet.LockReservation"), new { ReservationId = reservationId }, transaction))?.ToDomain();

    public async Task SaveAsync(Posting posting, StoredPosting record, IReadOnlyList<Account> changedAccounts, Reservation? reservation, bool isNewReservation)
    {
        var now = time.GetUtcNow();
        if (reservation is not null && isNewReservation)
        {
            await connection.ExecuteAsync(Sql.Get("Wallet.InsertReservation"), new
            {
                reservation.ReservationId, reservation.AccountId, reservation.Amount, reservation.Currency, reservation.Reference, State = (byte)reservation.State, Now = now,
            }, transaction);
        }
        else if (reservation is not null)
        {
            await connection.ExecuteAsync(Sql.Get("Wallet.UpdateReservation"), new { reservation.ReservationId, State = (byte)reservation.State, Now = now }, transaction);
        }

        await connection.ExecuteAsync(Sql.Get("Wallet.InsertPosting"), new
        {
            posting.PostingId, Kind = (byte)posting.Kind, posting.IdempotencyKey, record.AccountId, record.Amount, record.ReservationId, posting.Reference, PostedAt = now,
        }, transaction);
        await connection.ExecuteAsync(Sql.Get("Wallet.InsertEntry"), posting.Entries.Select(e => new { posting.PostingId, e.AccountId, Bucket = (byte)e.Bucket, e.Amount }), transaction);
        await connection.ExecuteAsync(Sql.Get("Wallet.UpdateAccount"), changedAccounts.Select(a => new { a.AccountId, a.Available, a.Reserved }), transaction);
    }

    public Task CommitAsync() => transaction.CommitAsync();

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}
