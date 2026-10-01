using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Config;
using SwiftBets.Placement.Infrastructure.Config;

namespace SwiftBets.Placement.Infrastructure.Tests;

public sealed class ConfigPlacementSettingsTests
{
    [Fact]
    public void Nothing_configured_restricts_nothing()
    {
        var settings = new ConfigPlacementSettings(new State());

        settings.IsStopped.ShouldBeFalse();
        settings.MaxStake("ZAR").ShouldBeNull();
        settings.MaxPayout("ZAR").ShouldBeNull();
    }

    [Theory]
    [InlineData("placement.kill-switch", "on", true)]
    [InlineData("placement.kill-switch", "off", false)]
    [InlineData("placement.mode", "closed", true)]
    [InlineData("placement.mode", "preMatchOnly", false)]
    public void The_kill_switch_or_a_closed_mode_stops_placement(string key, string value, bool stopped) =>
        new ConfigPlacementSettings(new State((key, value))).IsStopped.ShouldBe(stopped);

    [Fact]
    public void Limits_are_read_per_currency()
    {
        var settings = new ConfigPlacementSettings(new State((ConfigKeys.MaxStake("ZAR"), "500000"), (ConfigKeys.MaxPayout("USD"), "100000")));

        settings.MaxStake("ZAR").ShouldBe(500_000);
        settings.MaxStake("USD").ShouldBeNull();
        settings.MaxPayout("USD").ShouldBe(100_000);
    }

    private sealed class State(params (string Key, string Value)[] entries) : ICompactedState<ConfigEntryV1>
    {
        private readonly Dictionary<string, ConfigEntryV1> _values = entries.ToDictionary(
            e => e.Key, e => new ConfigEntryV1(e.Key, e.Value, 1, "ops-1", "test", DateTimeOffset.UnixEpoch), StringComparer.Ordinal);

        public bool IsReady => true;

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public bool TryGet(string key, out ConfigEntryV1 value) => _values.TryGetValue(key, out value!);

        public IReadOnlyDictionary<string, ConfigEntryV1> Snapshot() => _values;
    }
}
