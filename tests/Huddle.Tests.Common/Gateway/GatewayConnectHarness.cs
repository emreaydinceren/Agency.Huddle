using System.Net;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Every in-memory gateway service wired over one <c>team.db</c> in a temp directory and a <see cref="FakeTimeProvider"/>,
/// with helpers that issue a nonce, sign a handshake with a <see cref="TestDevice"/> and call the connect service.
/// </summary>
internal sealed class GatewayConnectHarness : IDisposable
{
    /// <summary>The configured gateway token.</summary>
    public const string GatewayToken = "0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>The configured gateway password.</summary>
    public const string Password = "correct horse battery staple";

    /// <summary>The fake clock's start.</summary>
    public static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();

    /// <summary>Builds the services. <paramref name="configure"/> edits the <c>Team:Gateway</c> keys (default: token mode with <see cref="GatewayToken"/>).</summary>
    /// <param name="configure">Edits the configuration dictionary before the settings are read.</param>
    public GatewayConnectHarness(Action<Dictionary<string, string?>>? configure = null)
    {
        Dictionary<string, string?> values = new(StringComparer.Ordinal)
        {
            ["Team:Gateway:Auth:Mode"] = "token",
            ["Team:Gateway:Auth:Token"] = GatewayToken,
        };
        configure?.Invoke(values);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        this.Clock = new FakeTimeProvider(Start);
        this.Settings = GatewaySettings.ReadForStartup(this.dir.Path, configuration, new FakeGatewayEnvironment(), this.Clock);
        this.Limiter = new AuthRateLimiter(this.Settings.Options.Auth.RateLimit, this.Clock);
        this.Nonces = new ChallengeNonceStore(this.Clock);
        this.Devices = new DeviceRegistry(this.dir.Path, this.Clock);
        this.Pairing = new PairingService(this.Devices, this.Clock);
        this.Bootstrap = new BootstrapTokenStore(this.Clock);
        this.Service = new GatewayConnectService(this.Settings, this.Limiter, this.Nonces, this.Devices, this.Pairing, this.Bootstrap, this.Clock);
    }

    /// <summary>The fake clock.</summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>The settings.</summary>
    public GatewaySettings Settings { get; }

    /// <summary>The limiter.</summary>
    public AuthRateLimiter Limiter { get; }

    /// <summary>The nonces.</summary>
    public ChallengeNonceStore Nonces { get; }

    /// <summary>The devices.</summary>
    public DeviceRegistry Devices { get; }

    /// <summary>The pairing service.</summary>
    public PairingService Pairing { get; }

    /// <summary>The bootstrap tokens.</summary>
    public BootstrapTokenStore Bootstrap { get; }

    /// <summary>The service under test.</summary>
    public GatewayConnectService Service { get; }

    /// <summary>The data directory holding <c>team.db</c>.</summary>
    public string DataDir => this.dir.Path;

    /// <summary>A loopback ingress.</summary>
    public static IngressResult Loopback { get; } = new(IngressKind.Loopback, IPAddress.Loopback, null, false, null);

    /// <summary>A direct non-loopback ingress (a LAN address).</summary>
    /// <param name="ip">The client address.</param>
    public static IngressResult Direct(string ip = "203.0.113.9") => new(IngressKind.Direct, IPAddress.Parse(ip), null, false, null);

    /// <summary>The managed (Tailscale Serve) listener with a verified login.</summary>
    /// <param name="login">The Tailscale login.</param>
    /// <param name="ip">The client address.</param>
    public static IngressResult Managed(string login = "ada@example.com", string ip = "100.64.0.7") => new(IngressKind.Managed, IPAddress.Parse(ip), login, false, null);

    /// <summary>The managed listener with the Funnel header and no identity.</summary>
    public static IngressResult Funnel { get; } = new(IngressKind.Managed, IPAddress.Parse("198.51.100.4"), null, true, null);

    /// <summary>Issues a nonce, signs a handshake for <paramref name="device"/> now, and runs it through the connect service.</summary>
    /// <param name="device">The device.</param>
    /// <param name="auth">The credentials.</param>
    /// <param name="ingress">The ingress.</param>
    /// <param name="scopes">The scopes asked for; default read and write.</param>
    /// <param name="hostAndOriginLoopback">Whether <c>Host</c> and <c>Origin</c> are loopback names.</param>
    public ConnectOutcome Connect(
        TestDevice device,
        ConnectAuth auth,
        IngressResult ingress,
        IReadOnlyList<string>? scopes = null,
        bool hostAndOriginLoopback = true)
    {
        ChallengeNonceStore.Challenge challenge = this.Nonces.Issue();
        ConnectRequest request = device.Build(
            challenge.Nonce,
            this.Clock.GetUtcNow().ToUnixTimeMilliseconds(),
            scopes ?? [GatewayScopes.Read, GatewayScopes.Write],
            auth);
        return this.Service.Connect(request, new ConnectContext(ingress, hostAndOriginLoopback, "test-agent"));
    }

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();
}
