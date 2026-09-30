using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Placement;
using SwiftBets.History.Application;

namespace SwiftBets.History.Infrastructure;

public sealed class PlacedProjector(IHistoryStore store) : IEventHandler<CouponPlacedV1>
{
    public Task HandleAsync(ConsumedEvent<CouponPlacedV1> message, CancellationToken cancellationToken) => store.ProjectPlacedAsync(message.Envelope.Payload, cancellationToken);
}
