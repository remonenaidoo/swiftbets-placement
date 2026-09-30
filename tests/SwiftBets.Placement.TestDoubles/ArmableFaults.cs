using SwiftBets.BuildingBlocks.Core;

namespace SwiftBets.Placement.TestDoubles;

public sealed class ArmableFaults : IFaultPoint
{
    public string? Armed { get; set; }

    public ValueTask HitAsync(string name, CancellationToken cancellationToken = default)
    {
        if (Armed == name)
        {
            Armed = null;
            throw new FaultInjectedException(name);
        }

        return ValueTask.CompletedTask;
    }
}
