using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Placement;
using SwiftBets.Placement.Application.Ports;
using SwiftBets.Placement.Domain.Coupons;

namespace SwiftBets.Placement.Infrastructure.Persistence;

/// <summary>Saga intents, coupons and their events. Every state change is conditional on the state it expects, so the live saga and the sweeper cannot both win.</summary>
public sealed class SqlCouponStore(ISqlConnectionFactory connections, IOutbox outbox, TimeProvider time) : ICouponStore
{
    private static readonly SqlResources Sql = SqlResources.For<SqlCouponStore>();

    public async Task<SagaIntent?> TryStartAsync(SagaIntent intent)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        try
        {
            await connection.ExecuteAsync(Sql.Get("Saga.Insert"), new { intent.CouponId, intent.PunterId, intent.IdempotencyKey, intent.RequestHash, intent.Stake, intent.Currency, intent.CreatedAt, intent.DeadlineAt });
            return null;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            return (await connection.QuerySingleAsync<SagaRow>(Sql.Get("Saga.FindByKey"), new { intent.PunterId, intent.IdempotencyKey })).ToIntent();
        }
    }

    public async Task<bool> TryMarkReservedAsync(Guid couponId, Guid reservationId)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        return await connection.ExecuteAsync(Sql.Get("Saga.MarkReserved"), new { CouponId = couponId, ReservationId = reservationId, Now = time.GetUtcNow() }) == 1;
    }

    public async Task MarkRejectedAsync(Guid couponId, int responseStatus, string responseJson, CouponRejectedV1 rejected)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        if (await connection.ExecuteAsync(Sql.Get("Saga.MarkRejected"), new { CouponId = couponId, ResponseStatus = responseStatus, ResponseJson = responseJson, Now = time.GetUtcNow() }, transaction) == 1)
        {
            await outbox.EnqueueAsync(transaction, Topics.CouponRejected, couponId.ToString(), Envelope(rejected), CancellationToken.None);
        }

        await transaction.CommitAsync();
    }

    public async Task<bool> TryPersistAsync(PlacedCoupon coupon, int responseStatus, string responseJson, CouponPlacedV1? placed, CouponPlacedV2 placedV2)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        if (await connection.ExecuteAsync(Sql.Get("Saga.MarkPersisted"), new { coupon.CouponId, ResponseStatus = responseStatus, ResponseJson = responseJson, Now = time.GetUtcNow() }, transaction) != 1)
        {
            await transaction.RollbackAsync();
            return false;
        }

        await connection.ExecuteAsync(Sql.Get("Coupon.Insert"), new
        {
            coupon.CouponId, coupon.PunterId, BetType = (byte)coupon.BetType, coupon.Stake, coupon.Currency, coupon.TotalOdds, coupon.PotentialPayout, coupon.PlacedAt,
        }, transaction);
        await connection.ExecuteAsync(Sql.Get("Coupon.InsertLeg"), coupon.Legs.Select(l => new { l.LegId, coupon.CouponId, l.FixtureId, l.MarketId, l.SelectionId, l.Odds, l.OfferVersion, l.IsBanker }), transaction);
        await connection.ExecuteAsync(Sql.Get("Coupon.InsertBet"), coupon.Bets.Select(b => new
        {
            b.BetId, coupon.CouponId, b.Name, Folds = string.Join(',', b.Folds), b.Lines, b.UnitStake, b.Stake, b.PotentialPayout,
        }), transaction);
        if (placed is not null)
        {
            await outbox.EnqueueAsync(transaction, Topics.CouponPlaced, coupon.CouponId.ToString(), Envelope(placed), CancellationToken.None);
        }

        await outbox.EnqueueAsync(transaction, Topics.CouponPlacedV2, coupon.CouponId.ToString(), Envelope(placedV2), CancellationToken.None);
        await transaction.CommitAsync();
        return true;
    }

    public async Task MarkCompletedAsync(Guid couponId)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Saga.MarkCompleted"), new { CouponId = couponId, Now = time.GetUtcNow() });
    }

    public async Task MarkCompensatedAsync(Guid couponId, int responseStatus, string responseJson)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(Sql.Get("Saga.MarkCompensated"), new { CouponId = couponId, ResponseStatus = responseStatus, ResponseJson = responseJson, Now = time.GetUtcNow() });
    }

    public async Task<IReadOnlyList<SagaIntent>> ClaimExpiredAsync(DateTimeOffset now, int batchSize, TimeSpan lease, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<SagaRow>(new CommandDefinition(Sql.Get("Saga.ClaimExpired"), new { BatchSize = batchSize, Now = now, LeaseUntil = now + lease }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToIntent())];
    }

    public async Task<IReadOnlyList<CouponDigest>> DigestAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return [.. await connection.QueryAsync<CouponDigest>(new CommandDefinition(Sql.Get("Coupon.IntegrityDigest"), new { From = from, To = to, Limit = limit }, cancellationToken: cancellationToken))];
    }

    public async Task<PlacedCoupon?> GetCouponAsync(Guid couponId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        using var reader = await connection.QueryMultipleAsync(new CommandDefinition(Sql.Get("Coupon.Get"), new { CouponId = couponId }, cancellationToken: cancellationToken));
        var coupon = await reader.ReadSingleOrDefaultAsync<(Guid CouponId, Guid PunterId, byte BetType, long Stake, string Currency, decimal TotalOdds, long PotentialPayout, DateTimeOffset PlacedAt)?>();
        if (coupon is not { } c)
        {
            return null;
        }

        var legs = (await reader.ReadAsync<AcceptedLeg>()).ToList();
        var bets = (await reader.ReadAsync<(Guid BetId, string Name, string Folds, int Lines, long UnitStake, long Stake, long PotentialPayout)>())
            .Select(b => new PricedBet(b.BetId, b.Name, [.. b.Folds.Split(',').Select(f => int.Parse(f, System.Globalization.CultureInfo.InvariantCulture))], b.Lines, b.UnitStake, b.Stake, b.PotentialPayout))
            .ToList();
        return new PlacedCoupon(c.CouponId, c.PunterId, (BetType)c.BetType, c.Stake, c.Currency, c.TotalOdds, c.PotentialPayout, legs, c.PlacedAt, bets);
    }

    private EventEnvelope<T> Envelope<T>(T payload)
        where T : IEventContract =>
        EventEnvelope<T>.Create(payload, time.GetUtcNow(), CorrelationContext.CorrelationId ?? CorrelationContext.NewId());
}
