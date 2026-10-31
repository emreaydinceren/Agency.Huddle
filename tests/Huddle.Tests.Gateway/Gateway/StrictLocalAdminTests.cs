using System.Security.Claims;
using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Strict local mode in <see cref="GatewayAdminService"/> (ra-strict-local.md): switching it on takes effect at once (sessions end, device
/// connections are aborted), switching it off is saved for the next start, and the host's own connection is never aborted.
/// </summary>
public sealed class StrictLocalAdminTests : IDisposable
{
    private readonly GatewayAdminHarness harness;
    private readonly SessionRegistry sessions;
    private readonly GatewayConnectionGuard guard;
    private readonly GatewayAdminService service;

    /// <summary>Starts with the key the test class names; the harness default is strict local off.</summary>
    public StrictLocalAdminTests()
    {
        this.harness = new GatewayAdminHarness();
        this.sessions = new SessionRegistry(this.harness.Devices, this.harness.Clock);
        this.guard = new GatewayConnectionGuard(this.harness.Devices, new CookieSessionResolver(this.sessions, this.harness.Running));
        this.service = new GatewayAdminService(
            this.harness.Store,
            this.harness.Running,
            this.harness.Devices,
            this.harness.Pairing,
            this.harness.Links,
            this.harness.Qr,
            this.harness.View,
            this.harness.Clock,
            this.sessions,
            this.guard);
    }

    /// <inheritdoc />
    public void Dispose() => this.harness.Dispose();

    private GatewayAdminService.Values Stored() => this.service.GetSettings(GatewayAdminHarness.Admin()).Stored;

    /// <summary>Switching it on applies now: it is listed as applied now, the running options change and nothing is left pending.</summary>
    [Fact]
    public void SwitchOn_AppliesNow()
    {
        GatewayAdminService.SaveResult result = this.service.SaveSettings(GatewayAdminHarness.Admin(), this.Stored() with { StrictLocal = true });

        Assert.Contains("Strict local", result.AppliedNow);
        Assert.DoesNotContain("Strict local", result.NextStart);
        Assert.True(this.harness.Running.Options.StrictLocal);
        Assert.Empty(this.service.GetSettings(GatewayAdminHarness.Admin()).Pending);
        Assert.True(this.SavedStrictLocal());
    }

    /// <summary>Switching it on ends every session.</summary>
    [Fact]
    public void SwitchOn_EndsEverySession()
    {
        DeviceRecord device = this.harness.PairDevice("phone");
        string cookie = this.sessions.Issue(device.DeviceId, GatewayScopes.Limited, "Direct", null);
        Assert.NotNull(this.sessions.TryGet(cookie));

        _ = this.service.SaveSettings(GatewayAdminHarness.Admin(), this.Stored() with { StrictLocal = true });

        Assert.Null(this.sessions.TryGet(cookie));
    }

    /// <summary>Switching it on aborts the Blazor connections of devices, and leaves the host's own connection open.</summary>
    [Fact]
    public async Task SwitchOn_AbortsDeviceConnectionsButNotTheHost()
    {
        DeviceRecord device = this.harness.PairDevice("phone");
        string cookie = this.sessions.Issue(device.DeviceId, GatewayScopes.Limited, "Direct", null);
        string hash = this.sessions.TryGet(cookie)?.Hash ?? string.Empty;
        FakeCallerContext remote = new("remote-1", new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, device.DeviceId), new Claim(GatewayClaims.Session, hash)],
            GatewayClaims.AuthenticationType)));
        FakeCallerContext host = new("host-1", GatewayPrincipal.CreateHost());
        await this.ConnectAsync(remote);
        await this.ConnectAsync(host);

        _ = this.service.SaveSettings(GatewayAdminHarness.Admin(), this.Stored() with { StrictLocal = true });

        Assert.True(remote.Aborted);
        Assert.False(host.Aborted);
    }

    /// <summary>Switching it off is saved and listed for the next start; the running options keep strict local until then.</summary>
    [Fact]
    public void SwitchOff_IsSavedAndNeedsARestart()
    {
        _ = this.service.SaveSettings(GatewayAdminHarness.Admin(), this.Stored() with { StrictLocal = true });

        GatewayAdminService.SaveResult result = this.service.SaveSettings(GatewayAdminHarness.Admin(), this.Stored() with { StrictLocal = false });

        Assert.Contains("Strict local", result.NextStart);
        Assert.DoesNotContain("Strict local", result.AppliedNow);
        Assert.True(this.harness.Running.Options.StrictLocal);
        Assert.False(this.SavedStrictLocal());
        Assert.Contains(this.service.GetSettings(GatewayAdminHarness.Admin()).Pending, static change => change.Setting == "Strict local");
    }

    private bool SavedStrictLocal()
    {
        using System.Text.Json.JsonDocument saved = System.Text.Json.JsonDocument.Parse(this.harness.Store.StoredOverrides);
        return saved.RootElement.GetProperty("StrictLocal").GetBoolean();
    }

    private Task ConnectAsync(FakeCallerContext caller)
    {
        HubLifetimeContext context = new(caller, new ServiceProviderStub(), new TestHub());
        return this.guard.OnConnectedAsync(context, static _ => Task.CompletedTask);
    }

    private sealed class TestHub : Hub;

    private sealed class ServiceProviderStub : IServiceProvider
    {
        /// <inheritdoc />
        public object? GetService(Type serviceType) => null;
    }

    private sealed class FakeCallerContext(string connectionId, ClaimsPrincipal user) : HubCallerContext
    {
        /// <summary>Whether the guard aborted the connection.</summary>
        public bool Aborted { get; private set; }

        /// <inheritdoc />
        public override string ConnectionId { get; } = connectionId;

        /// <inheritdoc />
        public override string? UserIdentifier => null;

        /// <inheritdoc />
        public override ClaimsPrincipal? User { get; } = user;

        /// <inheritdoc />
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        /// <inheritdoc />
        public override IFeatureCollection Features { get; } = new FeatureCollection();

        /// <inheritdoc />
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        /// <inheritdoc />
        public override void Abort() => this.Aborted = true;
    }
}
