using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Manual-run fix (bug 6): a device in use reads "seen now", because its session checks touch its last-seen, at most once a minute.</summary>
public sealed class SessionRegistryLastSeenTests : IDisposable
{
    private static readonly IReadOnlyList<string> ReadWrite = [GatewayScopes.Read, GatewayScopes.Write];

    private readonly GatewayConnectHarness harness = new();

    /// <inheritdoc />
    public void Dispose() => this.harness.Dispose();

    private DateTimeOffset SeenAt(string deviceId) => (this.harness.Devices.Find(deviceId) ?? throw new InvalidOperationException("No such device.")).LastSeenAt;

    /// <summary>The gate's session check and the hub guard's liveness check both touch last-seen, and a second check inside a minute writes nothing.</summary>
    [Fact]
    public void SessionChecks_TouchLastSeenAtMostOncePerMinute()
    {
        SessionRegistry registry = new(this.harness.Devices, this.harness.Clock);
        TestDevice device = new();
        _ = this.harness.Devices.Pair(new DeviceRegistry.Registration(
            device.DeviceId, device.PublicKey, "Phone", ReadWrite, "approval", null, "203.0.113.9", null));
        string cookie = registry.Issue(device.DeviceId, ReadWrite, "Direct", null);
        GatewaySession session = registry.TryGet(cookie) ?? throw new InvalidOperationException("No session.");

        this.harness.Clock.Advance(TimeSpan.FromMinutes(5));
        _ = registry.TryGet(cookie);
        DateTimeOffset afterGate = this.SeenAt(device.DeviceId);

        this.harness.Clock.Advance(TimeSpan.FromSeconds(30));
        _ = registry.TryGetByHash(session.Hash);
        DateTimeOffset withinMinute = this.SeenAt(device.DeviceId);

        this.harness.Clock.Advance(TimeSpan.FromSeconds(31));
        _ = registry.TryGetByHash(session.Hash);
        DateTimeOffset afterMinute = this.SeenAt(device.DeviceId);

        Assert.Equal(GatewayConnectHarness.Start + TimeSpan.FromMinutes(5), afterGate);
        Assert.Equal(afterGate, withinMinute);
        Assert.Equal(GatewayConnectHarness.Start + TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(61), afterMinute);
    }
}
