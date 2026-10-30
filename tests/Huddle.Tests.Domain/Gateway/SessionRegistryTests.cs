using System.Security.Cryptography;
using System.Text;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.8, 8.5, 7.2: the in-memory sessions, their 12-hour life, what ends them and the Tailscale binding.</summary>
public sealed class SessionRegistryTests : IDisposable
{
    private static readonly IReadOnlyList<string> ReadWrite = [GatewayScopes.Read, GatewayScopes.Write];

    private readonly GatewayConnectHarness harness = new();

    /// <inheritdoc />
    public void Dispose() => this.harness.Dispose();

    private SessionRegistry NewRegistry() => new(this.harness.Devices, this.harness.Clock);

    private TestDevice PairDevice()
    {
        TestDevice device = new();
        _ = this.harness.Devices.Pair(new DeviceRegistry.Registration(
            device.DeviceId, device.PublicKey, "Phone", ReadWrite, "approval", null, "203.0.113.9", null));
        return device;
    }

    /// <summary>The cookie is 32 random bytes as base64url, and the registry keeps and exposes only its SHA-256 hash.</summary>
    [Fact]
    public void Issue_ReturnsRandomCookieAndKeepsOnlyItsHash()
    {
        SessionRegistry registry = this.NewRegistry();
        TestDevice device = this.PairDevice();

        string first = registry.Issue(device.DeviceId, ReadWrite, "Loopback", null);
        string second = registry.Issue(device.DeviceId, ReadWrite, "Loopback", null);
        GatewaySession? session = registry.TryGet(first);

        Assert.NotEqual(first, second);
        Assert.Equal(32, System.Buffers.Text.Base64Url.DecodeFromChars(first).Length);
        Assert.NotNull(session);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first))), session.Hash);
        Assert.Equal(device.DeviceId, session.DeviceId);
        Assert.Equal(ReadWrite, session.Scopes);
        Assert.Equal("Loopback", session.Ingress);
    }

    /// <summary>A cookie that is missing, empty or unknown names no session.</summary>
    /// <param name="cookie">The cookie value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("never-issued")]
    public void TryGet_NoSuchSession_ReturnsNull(string? cookie)
    {
        SessionRegistry registry = this.NewRegistry();

        Assert.Null(registry.TryGet(cookie));
    }

    /// <summary>A session lives 12 hours: valid just before, gone just after (older than 12 h is none).</summary>
    [Fact]
    public void TryGet_AfterTwelveHours_ReturnsNull()
    {
        SessionRegistry registry = this.NewRegistry();
        string cookie = registry.Issue(this.PairDevice().DeviceId, ReadWrite, "Loopback", null);

        this.harness.Clock.Advance(TimeSpan.FromHours(12) - TimeSpan.FromSeconds(1));
        GatewaySession? before = registry.TryGet(cookie);
        this.harness.Clock.Advance(TimeSpan.FromSeconds(2));
        GatewaySession? after = registry.TryGet(cookie);

        Assert.NotNull(before);
        Assert.Null(after);
        Assert.Equal(TimeSpan.FromHours(12), SessionRegistry.Lifetime);
    }

    /// <summary>Revoking, removing or rotating a device ends its sessions at once, and only that device's.</summary>
    /// <param name="change">What happens to the device.</param>
    [Theory]
    [InlineData("revoke")]
    [InlineData("remove")]
    [InlineData("rotate")]
    public void DeviceChange_EndsItsSessionsOnly(string change)
    {
        SessionRegistry registry = this.NewRegistry();
        TestDevice changed = this.PairDevice();
        TestDevice other = this.PairDevice();
        string changedCookie = registry.Issue(changed.DeviceId, ReadWrite, "Loopback", null);
        string changedSecond = registry.Issue(changed.DeviceId, ReadWrite, "Direct", null);
        string otherCookie = registry.Issue(other.DeviceId, ReadWrite, "Loopback", null);

        _ = change switch
        {
            "revoke" => this.harness.Devices.Revoke(changed.DeviceId),
            "remove" => this.harness.Devices.Remove(changed.DeviceId),
            _ => this.harness.Devices.Rotate(changed.DeviceId) is not null,
        };

        Assert.Null(registry.TryGet(changedCookie));
        Assert.Null(registry.TryGet(changedSecond));
        Assert.NotNull(registry.TryGet(otherCookie));
    }

    /// <summary>A session issued for a device that is not paired is none (Spec 8.5: the device must still be paired).</summary>
    [Fact]
    public void TryGet_DeviceNeverPaired_ReturnsNull()
    {
        SessionRegistry registry = this.NewRegistry();
        string cookie = registry.Issue(new TestDevice().DeviceId, ReadWrite, "Loopback", null);

        Assert.Null(registry.TryGet(cookie));
    }

    /// <summary>A managed session bound to a Tailscale login needs that login now (ignoring case); another login or none refuses it.</summary>
    [Fact]
    public void TryGet_ManagedSessionBoundToLogin_NeedsTheSameLoginIgnoringCase()
    {
        SessionRegistry registry = this.NewRegistry();
        string cookie = registry.Issue(this.PairDevice().DeviceId, ReadWrite, IngressKindText.Of(IngressKind.Managed), "ada@example.com");

        Assert.NotNull(registry.TryGet(cookie, "ada@example.com"));
        Assert.NotNull(registry.TryGet(cookie, "ADA@Example.com"));
        Assert.Null(registry.TryGet(cookie, "grace@example.com"));
        Assert.Null(registry.TryGet(cookie, null));
        Assert.Null(registry.TryGet(cookie));
    }

    /// <summary>A session that is not managed, or has no login, does not look at the current login.</summary>
    [Fact]
    public void TryGet_NotBoundToLogin_IgnoresTheCurrentLogin()
    {
        SessionRegistry registry = this.NewRegistry();
        TestDevice device = this.PairDevice();
        string loopback = registry.Issue(device.DeviceId, ReadWrite, IngressKindText.Of(IngressKind.Loopback), null);
        string managedNoLogin = registry.Issue(device.DeviceId, ReadWrite, IngressKindText.Of(IngressKind.Managed), null);

        Assert.NotNull(registry.TryGet(loopback, "anyone@example.com"));
        Assert.NotNull(registry.TryGet(managedNoLogin, "anyone@example.com"));
        Assert.NotNull(registry.TryGet(managedNoLogin));
    }

    /// <summary>The open-socket re-check by hash (C-5) answers the session while it is live and none after expiry or revocation; it never asks for a login.</summary>
    [Fact]
    public void TryGetByHash_LiveThenExpiredOrRevoked_ReturnsNullAfter()
    {
        SessionRegistry registry = this.NewRegistry();
        TestDevice device = this.PairDevice();
        string cookie = registry.Issue(device.DeviceId, ReadWrite, IngressKindText.Of(IngressKind.Managed), "ada@example.com");
        string hash = registry.TryGet(cookie, "ada@example.com")?.Hash ?? string.Empty;

        GatewaySession? live = registry.TryGetByHash(hash);
        _ = this.harness.Devices.Revoke(device.DeviceId);
        GatewaySession? revoked = registry.TryGetByHash(hash);
        string second = registry.Issue(this.PairDevice().DeviceId, ReadWrite, "Loopback", null);
        string secondHash = registry.TryGet(second)?.Hash ?? string.Empty;
        this.harness.Clock.Advance(TimeSpan.FromHours(13));

        Assert.NotNull(live);
        Assert.Null(revoked);
        Assert.Null(registry.TryGetByHash(secondHash));
        Assert.Null(registry.TryGetByHash(null));
    }
}
