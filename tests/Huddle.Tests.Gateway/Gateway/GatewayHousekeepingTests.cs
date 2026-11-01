using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Feed;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 10: the <c>GatewayHousekeeping</c> worker runs the four prunes (stale devices, limiter, sessions, pairing requests) on its own, each in isolation.</summary>
public sealed class GatewayHousekeepingTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyList<string> ReadWrite = [GatewayScopes.Read, GatewayScopes.Write];

    private readonly TempDataDir dir = new();
    private readonly FakeTimeProvider clock = new(Start);
    private readonly RecordingFeedPublisher feed = new();
    private readonly DeviceRegistry devices;
    private readonly AuthRateLimiter limiter;
    private readonly SessionRegistry sessions;
    private readonly PairingService pairing;
    private readonly GatewayHousekeeping housekeeping;

    /// <summary>Wires the four collaborators to one fake clock and one recording Feed.</summary>
    public GatewayHousekeepingTests()
    {
        GatewayFeed gatewayFeed = new(this.feed);
        this.devices = new DeviceRegistry(this.dir.Path, this.clock, gatewayFeed);
        this.limiter = new AuthRateLimiter(new AuthRateLimitOptions { MaxAttempts = 10, WindowSeconds = 60, LockoutSeconds = 300, ExemptLoopback = true }, this.clock);
        this.sessions = new SessionRegistry(this.devices, this.clock);
        this.pairing = new PairingService(this.devices, this.clock, gatewayFeed);
        this.housekeeping = new GatewayHousekeeping(this.devices, this.limiter, this.sessions, this.pairing, NullLogger<GatewayHousekeeping>.Instance, this.clock);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.housekeeping.Dispose();
        this.dir.Dispose();
    }

    private static PairingService.Candidate Candidate(TestDevice device)
        => new(device.DeviceId, device.PublicKey, ReadWrite, "iPad", "203.0.113.9", "ua/1", "Direct", null);

    private TestDevice Pair(string via)
    {
        TestDevice device = new();
        _ = this.devices.Pair(new DeviceRegistry.Registration(device.DeviceId, device.PublicKey, "Phone", ReadWrite, via, null, "203.0.113.9", "ua/1"));
        return device;
    }

    private void FailOnce(string client)
        => _ = this.limiter.RecordFailure(AuthRateLimiter.Key(AuthRateLimitScopes.SharedSecret, client), loopback: false);

    /// <summary>The interval is 60 seconds, as Spec 10 says.</summary>
    [Fact]
    public void Interval_IsSixtySeconds() => Assert.Equal(TimeSpan.FromSeconds(60), GatewayHousekeeping.Interval);

    /// <summary>C-11: a loopback device unseen for 31 days is deleted; one seen a day ago and a device paired by approval are kept.</summary>
    [Fact]
    public void RunOnce_DeletesStaleLoopbackDevice_KeepsRecentAndApproved()
    {
        TestDevice stale = this.Pair("loopback");
        TestDevice approved = this.Pair("approval");
        this.clock.Advance(TimeSpan.FromDays(30));
        TestDevice recent = this.Pair("loopback");
        this.clock.Advance(TimeSpan.FromDays(1));

        this.housekeeping.RunOnce();

        Assert.Null(this.devices.Find(stale.DeviceId));
        Assert.NotNull(this.devices.Find(recent.DeviceId));
        Assert.NotNull(this.devices.Find(approved.DeviceId));
    }

    /// <summary>An expired session is dropped by the sweep, so a later prune finds none; a live session is kept.</summary>
    [Fact]
    public void RunOnce_DropsExpiredSession_KeepsLiveOne()
    {
        TestDevice device = this.Pair("approval");
        string old = this.sessions.Issue(device.DeviceId, ReadWrite, "Direct", null);
        this.clock.Advance(SessionRegistry.Lifetime - TimeSpan.FromMinutes(1));
        string live = this.sessions.Issue(device.DeviceId, ReadWrite, "Direct", null);
        this.clock.Advance(TimeSpan.FromMinutes(2));

        this.housekeeping.RunOnce();

        Assert.False(this.sessions.IsValid(old));
        Assert.Equal(0, this.sessions.PruneExpired());
        Assert.True(this.sessions.IsValid(live));
    }

    /// <summary>A pairing request that lapsed is announced to the Feed by the sweep, with no other call into the service.</summary>
    [Fact]
    public void RunOnce_ExpiredPairingRequest_ResolvesItsFeedCard()
    {
        Assert.NotNull(this.pairing.Open(Candidate(new TestDevice())));
        this.clock.Advance(PairingService.RequestLifetime + TimeSpan.FromSeconds(1));
        Assert.Empty(this.feed.Resolutions);

        this.housekeeping.RunOnce();

        _ = Assert.Single(this.feed.Resolutions);
    }

    /// <summary>A limiter entry whose window has passed is pruned; a fresh one stays.</summary>
    [Fact]
    public void RunOnce_PrunesLimiterEntryWhoseWindowPassed()
    {
        this.FailOnce("198.51.100.1");
        this.clock.Advance(TimeSpan.FromSeconds(61));
        this.FailOnce("198.51.100.2");
        Assert.Equal(2, this.limiter.Count);

        this.housekeeping.RunOnce();

        Assert.Equal(1, this.limiter.Count);
    }

    /// <summary>One duty throwing (the device table is gone) does not stop the limiter, sessions or pairing sweep, and does not escape.</summary>
    [Fact]
    public void RunOnce_DeviceDutyThrows_OtherDutiesStillRun()
    {
        TestDevice device = this.Pair("approval");
        string cookie = this.sessions.Issue(device.DeviceId, ReadWrite, "Direct", null);
        this.FailOnce("198.51.100.1");
        Assert.NotNull(this.pairing.Open(Candidate(new TestDevice())));
        using (SqliteConnection connection = new($"Data Source={Path.Combine(this.dir.Path, "team.db")}"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DROP TABLE gateway_devices;";
            _ = command.ExecuteNonQuery();
        }

        this.clock.Advance(SessionRegistry.Lifetime + PairingService.RequestLifetime);

        this.housekeeping.RunOnce();

        Assert.Equal(0, this.limiter.Count);
        Assert.False(this.sessions.IsValid(cookie));
        Assert.Equal(0, this.sessions.PruneExpired());
        _ = Assert.Single(this.feed.Resolutions);
    }

    /// <summary>The loop runs the duties once per interval of the injected clock, and stops quietly when cancelled.</summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task ExecuteAsync_RunsEveryInterval_AndStopsQuietlyOnCancel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.FailOnce("198.51.100.1");
        await this.housekeeping.StartAsync(ct);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (this.limiter.Count != 0 && DateTimeOffset.UtcNow < deadline)
        {
            // The loop creates its timer on its own thread, so keep moving the fake clock until a tick lands after that.
            this.clock.Advance(TimeSpan.FromSeconds(61));
            await Task.Delay(20, ct);
        }

        Assert.Equal(0, this.limiter.Count);
        await this.housekeeping.StopAsync(ct);
        Assert.True(this.housekeeping.ExecuteTask is { IsCompletedSuccessfully: true });
    }
}
