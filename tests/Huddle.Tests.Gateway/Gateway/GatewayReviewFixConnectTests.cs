using System.Net;
using System.Security.Claims;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Review fixes R-1, R-2, R-4, R-10 and R-11: the denylist, rotate hand-off, trusted-proxy auto-approve, Funnel and the PBKDF2 guards.</summary>
public sealed class GatewayReviewFixConnectTests
{
    private static readonly string[] ReadOnly = [GatewayScopes.Read];

    private static ConnectAuth Token() => new(Token: GatewayConnectHarness.GatewayToken);

    private static IngressResult Proxy() => new(IngressKind.TrustedProxy, IPAddress.Parse("198.51.100.20"), null, false, null) { ProxyUser = "ada" };

    private static string PairViaTailscale(GatewayConnectHarness harness, TestDevice device)
    {
        ConnectOutcome first = harness.Connect(device, new ConnectAuth(), GatewayConnectHarness.Managed());
        Assert.Equal(ConnectOutcome.Kind.Connected, first.Result);
        Assert.False(string.IsNullOrEmpty(first.DeviceToken));
        return first.DeviceToken;
    }

    private static PairingPolicy.Input PolicyInput(IngressKind ingress, bool funnel, BootstrapTokenStore.Kind? bootstrap, bool revoked = false) => new(
        ingress,
        HostAndOriginLoopback: true,
        AutoApproveLocal: true,
        GatewayAuthPassed: true,
        TailscaleLogin: "ada@example.com",
        AllowTailscale: true,
        IsFunnel: funnel,
        bootstrap,
        TrustedProxy: null,
        [GatewayScopes.Read, GatewayScopes.Write],
        Revoked: revoked);

    /// <summary>R-1: a device revoked after a Tailscale pairing no longer auto-pairs with the same key and identity; it gets a pairing request and no token.</summary>
    [Fact]
    public void Connect_RevokedTailscaleDevice_RequiresPairingNotToken()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        _ = PairViaTailscale(harness, device);
        _ = harness.Devices.Revoke(device.DeviceId);

        ConnectOutcome again = harness.Connect(device, new ConnectAuth(), GatewayConnectHarness.Managed());

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, again.Result);
        Assert.Equal(ConnectOutcome.PairingRequired, again.Code);
        Assert.Null(again.DeviceToken);
        Assert.False(harness.Devices.Find(device.DeviceId)?.HasToken);
        Assert.Single(harness.Pairing.Pending());
    }

    /// <summary>R-1: a revoked loopback device does not auto-pair again either.</summary>
    [Fact]
    public void Connect_RevokedLoopbackDevice_RequiresPairing()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        _ = harness.Connect(device, Token(), GatewayConnectHarness.Loopback);
        _ = harness.Devices.Revoke(device.DeviceId);

        ConnectOutcome again = harness.Connect(device, Token(), GatewayConnectHarness.Loopback);

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, again.Result);
        Assert.Null(again.DeviceToken);
    }

    /// <summary>R-1: a removed device is denylisted the same way as a revoked one.</summary>
    [Fact]
    public void Connect_RemovedDevice_RequiresPairing()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        _ = PairViaTailscale(harness, device);
        Assert.True(harness.Devices.Remove(device.DeviceId));

        ConnectOutcome again = harness.Connect(device, new ConnectAuth(), GatewayConnectHarness.Managed());

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, again.Result);
        Assert.Null(harness.Devices.Find(device.DeviceId));
    }

    /// <summary>R-1: the denylist is persisted in <c>team.db</c> and survives a new registry over the same directory.</summary>
    [Fact]
    public void DeviceRegistry_Revoked_SurvivesReopen()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        _ = PairViaTailscale(harness, device);
        _ = harness.Devices.Revoke(device.DeviceId);

        DeviceRegistry reopened = new(harness.DataDir, harness.Clock);

        Assert.True(reopened.IsRevoked(device.DeviceId));
        Assert.False(reopened.IsRevoked(new TestDevice().DeviceId));
    }

    /// <summary>R-1: an admin bootstrap token still pairs a denylisted device (the caller is local), and that pairing clears the entry.</summary>
    [Fact]
    public void Connect_RevokedDeviceWithAdminBootstrap_PairsAndClearsDenylist()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        _ = PairViaTailscale(harness, device);
        _ = harness.Devices.Revoke(device.DeviceId);
        string bootstrap = harness.Bootstrap.Issue(BootstrapTokenStore.Kind.Admin);

        ConnectOutcome outcome = harness.Connect(device, new ConnectAuth(BootstrapToken: bootstrap), GatewayConnectHarness.Direct());

        Assert.Equal(ConnectOutcome.Kind.Connected, outcome.Result);
        Assert.Equal("bootstrap", outcome.PairedVia);
        Assert.False(harness.Devices.IsRevoked(device.DeviceId));
    }

    /// <summary>R-1: a human approving the pairing request clears the denylist entry, so the device then connects.</summary>
    [Fact]
    public void Approve_RevokedDevice_ClearsDenylistAndTheDeviceConnects()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        _ = PairViaTailscale(harness, device);
        _ = harness.Devices.Revoke(device.DeviceId);
        ConnectOutcome pending = harness.Connect(device, new ConnectAuth(), GatewayConnectHarness.Managed());
        Assert.Equal(ConnectOutcome.Kind.PairingRequired, pending.Result);

        PairingService.ApprovalResult approval = harness.Pairing.Approve(pending.RequestId ?? string.Empty, GatewayScopes.AdminSet);
        ConnectOutcome connected = harness.Connect(device, new ConnectAuth(), GatewayConnectHarness.Managed());

        Assert.Equal(PairingService.ApprovalOutcome.Approved, approval.Outcome);
        Assert.False(harness.Devices.IsRevoked(device.DeviceId));
        Assert.Equal(ConnectOutcome.Kind.Connected, connected.Result);
        Assert.False(string.IsNullOrEmpty(connected.DeviceToken));
    }

    /// <summary>R-2: rotating through the admin service hands the new token to the device's next connect, and the old token stops working.</summary>
    [Fact]
    public void Rotate_ThroughAdminService_NextConnectCarriesTheNewToken()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        string oldToken = harness.Connect(device, Token(), GatewayConnectHarness.Loopback).DeviceToken ?? string.Empty;
        GatewayAdminService admin = new(
            new GatewaySettingsStore(harness.DataDir, harness.Clock),
            harness.Settings,
            harness.Devices,
            harness.Pairing,
            new BootstrapLinks(harness.Bootstrap),
            new RecordingQrEncoder(),
            new FakeGatewayRuntimeView(),
            harness.Clock);
        ClaimsPrincipal caller = GatewayAdminHarness.Admin();

        bool rotated = admin.Rotate(caller, device.DeviceId);
        ConnectOutcome handOff = harness.Connect(device, Token(), GatewayConnectHarness.Direct());
        ConnectOutcome second = harness.Connect(device, Token(), GatewayConnectHarness.Direct());
        ConnectOutcome viaNew = harness.Connect(device, new ConnectAuth(DeviceToken: handOff.DeviceToken), GatewayConnectHarness.Direct());
        ConnectOutcome viaOld = harness.Connect(device, new ConnectAuth(DeviceToken: oldToken), GatewayConnectHarness.Direct());

        Assert.True(rotated);
        Assert.Equal(ConnectOutcome.Kind.Connected, handOff.Result);
        Assert.False(string.IsNullOrEmpty(handOff.DeviceToken));
        Assert.NotEqual(oldToken, handOff.DeviceToken);
        Assert.Null(second.DeviceToken);
        Assert.Equal(ConnectOutcome.Kind.Connected, viaNew.Result);
        Assert.Equal(ConnectOutcome.DeviceTokenMismatch, viaOld.Code);
    }

    /// <summary>R-4: trusted-proxy device auto-approve is off by default, so a proxied device gets a pairing request.</summary>
    [Fact]
    public void Connect_TrustedProxyDefault_RequiresPairing()
    {
        using GatewayConnectHarness harness = new(config => config["Team:Gateway:Auth:Mode"] = "trusted-proxy");

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(), Proxy());

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, outcome.Result);
    }

    /// <summary>R-4: with <c>TrustedProxy:DeviceAutoApprove:Enabled</c> a proxied device pairs via <c>trusted-proxy</c> with the default read and write scopes.</summary>
    [Fact]
    public void Connect_TrustedProxyAutoApproveEnabled_PairsWithDefaultScopes()
    {
        using GatewayConnectHarness harness = new(config =>
        {
            config["Team:Gateway:Auth:Mode"] = "trusted-proxy";
            config["Team:Gateway:TrustedProxy:DeviceAutoApprove:Enabled"] = "true";
        });

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(), Proxy());

        Assert.Equal(ConnectOutcome.Kind.Connected, outcome.Result);
        Assert.Equal("trusted-proxy", outcome.PairedVia);
        Assert.Equal([GatewayScopes.Read, GatewayScopes.Write], outcome.Scopes.Order(StringComparer.Ordinal));
    }

    /// <summary>R-4: the configured scope cap limits what the proxy auto-approves.</summary>
    [Fact]
    public void Connect_TrustedProxyAutoApproveCapped_GrantsOnlyTheCap()
    {
        using GatewayConnectHarness harness = new(config =>
        {
            config["Team:Gateway:Auth:Mode"] = "trusted-proxy";
            config["Team:Gateway:TrustedProxy:DeviceAutoApprove:Enabled"] = "true";
            config["Team:Gateway:TrustedProxy:DeviceAutoApprove:Scopes:0"] = GatewayScopes.Read;
        });

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(), Proxy());

        Assert.Equal(ConnectOutcome.Kind.Connected, outcome.Result);
        Assert.Equal(ReadOnly, outcome.Scopes);
    }

    /// <summary>R-4: <c>AllowRealIpFallback</c> binds and defaults to false.</summary>
    [Fact]
    public void GatewayOptions_AllowRealIpFallback_DefaultsFalseAndBinds()
    {
        using GatewayConnectHarness off = new();
        using GatewayConnectHarness on = new(config => config["Team:Gateway:AllowRealIpFallback"] = "true");

        Assert.False(off.Settings.Options.AllowRealIpFallback);
        Assert.True(on.Settings.Options.AllowRealIpFallback);
    }

    /// <summary>R-10: Funnel never auto-approves, an admin bootstrap token included.</summary>
    [Fact]
    public void Decide_Funnel_NeverAutoApproves()
    {
        PairingPolicy.Decision plain = PairingPolicy.Decide(PolicyInput(IngressKind.Managed, funnel: true, bootstrap: null));
        PairingPolicy.Decision admin = PairingPolicy.Decide(PolicyInput(IngressKind.Managed, funnel: true, BootstrapTokenStore.Kind.Admin));

        Assert.False(plain.AutoApprove);
        Assert.False(admin.AutoApprove);
    }

    /// <summary>R-1: a denylisted device is not auto-approved by loopback or Tailscale, but an admin bootstrap still approves it.</summary>
    [Fact]
    public void Decide_RevokedDevice_OnlyAdminBootstrapApproves()
    {
        Assert.False(PairingPolicy.Decide(PolicyInput(IngressKind.Loopback, funnel: false, bootstrap: null, revoked: true)).AutoApprove);
        Assert.False(PairingPolicy.Decide(PolicyInput(IngressKind.Managed, funnel: false, bootstrap: null, revoked: true)).AutoApprove);
        Assert.True(PairingPolicy.Decide(PolicyInput(IngressKind.Direct, funnel: false, BootstrapTokenStore.Kind.Admin, revoked: true)).AutoApprove);
    }

    /// <summary>R-11: failures from two addresses in one IPv6 /64 share a lockout, and another /64 is unaffected.</summary>
    [Fact]
    public void Connect_Ipv6ClientsInOneSlash64_ShareTheRateLimit()
    {
        using GatewayConnectHarness harness = new(config => config["Team:Gateway:Auth:RateLimit:MaxAttempts"] = "3");
        ConnectAuth wrong = new(Token: "wrong");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(attempt < 2 ? 401 : 429, harness.Connect(new TestDevice(), wrong, GatewayConnectHarness.Direct("2001:db8:1:2::1")).HttpStatus);
        }

        ConnectOutcome sameNetwork = harness.Connect(new TestDevice(), wrong, GatewayConnectHarness.Direct("2001:db8:1:2:aaaa:bbbb:cccc:dddd"));
        ConnectOutcome otherNetwork = harness.Connect(new TestDevice(), wrong, GatewayConnectHarness.Direct("2001:db8:1:3::1"));

        Assert.Equal(429, sameNetwork.HttpStatus);
        Assert.Equal(AuthRateLimiter.ErrorCode, sameNetwork.Code);
        Assert.Equal(401, otherNetwork.HttpStatus);
    }

    /// <summary>R-11: the gate admits at most the configured number of concurrent verifications; the rest wait, then are refused.</summary>
    [Fact]
    public void PasswordVerificationGate_AtCapacity_RefusesTheExcess()
    {
        PasswordVerificationGate gate = new(maxConcurrent: 2, wait: TimeSpan.FromMilliseconds(20));

        IDisposable? first = gate.TryEnter();
        IDisposable? second = gate.TryEnter();
        IDisposable? third = gate.TryEnter();
        first?.Dispose();
        IDisposable? afterRelease = gate.TryEnter();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(third);
        Assert.NotNull(afterRelease);
        Assert.Equal(4, PasswordVerificationGate.DefaultMaxConcurrent);
    }

    /// <summary>R-11: with the verification gate full, a password connect fails as a rate limit and records no failure.</summary>
    [Fact]
    public void Connect_PasswordGateFull_FailsAsRateLimited()
    {
        using GatewayConnectHarness harness = new(config =>
        {
            config["Team:Gateway:Auth:Mode"] = "password";
            config["Team:Gateway:Auth:Password"] = GatewayConnectHarness.Password;
        });
        PasswordVerificationGate gate = new(maxConcurrent: 1, wait: TimeSpan.Zero);
        GatewayConnectService service = new(harness.Settings, harness.Limiter, harness.Nonces, harness.Devices, harness.Pairing, harness.Bootstrap, harness.Clock, gate);
        TestDevice device = new();
        ConnectRequest Build() => device.Build(
            harness.Nonces.Issue().Nonce,
            harness.Clock.GetUtcNow().ToUnixTimeMilliseconds(),
            [GatewayScopes.Read, GatewayScopes.Write],
            new ConnectAuth(Password: GatewayConnectHarness.Password));
        ConnectContext context = new(GatewayConnectHarness.Direct(), true, "test-agent");

        IDisposable? held = gate.TryEnter();
        ConnectOutcome blocked = service.Connect(Build(), context);
        held?.Dispose();
        ConnectOutcome free = service.Connect(Build(), context);

        Assert.Equal(429, blocked.HttpStatus);
        Assert.Equal(AuthRateLimiter.ErrorCode, blocked.Code);
        Assert.NotEqual(ConnectOutcome.Kind.Failed, free.Result);
    }
}
