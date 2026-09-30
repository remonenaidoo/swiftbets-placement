using System.Data;
using Dapper;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Wallet.Application.Ports;
using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Infrastructure.Persistence;

public sealed class SqlWalletStore(ISqlConnectionFactory connections, TimeProvider time) : IWalletStore
{
    private static readonly SqlResources Sql = SqlResources.For<SqlWalletStore>();

    public async Task<IWalletTransaction> BeginAsync()
    {
        var connection = await connections.OpenAsync(CancellationToken.None);
        var transaction = (Microsoft.Data.SqlClient.SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        return new SqlWalletTransaction(connection, transaction, time);
    }

    public async Task<Account?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<AccountRow>(new CommandDefinition(Sql.Get("Wallet.GetAccount"), new { AccountId = accountId }, cancellationToken: cancellationToken)))?.ToDomain();
    }

    public async Task<Reservation?> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<ReservationRow>(new CommandDefinition(Sql.Get("Wallet.GetReservation"), new { ReservationId = reservationId }, cancellationToken: cancellationToken)))?.ToDomain();
    }

    public async Task<Reservation?> FindReservationByKeyAsync(string reserveIdempotencyKey, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<ReservationRow>(new CommandDefinition(Sql.Get("Wallet.FindReservationByKey"), new { IdempotencyKey = reserveIdempotencyKey }, cancellationToken: cancellationToken)))?.ToDomain();
    }
}
