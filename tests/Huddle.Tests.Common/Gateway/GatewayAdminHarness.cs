using System.Security.Claims;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// A <see cref="GatewayAdminService"/> over real stores in a temporary directory, with a fake clock, QR encoder and runtime view.
/// Shared by the Domain tests of the service and the bUnit tests of the Remote access panel (Stage E1).
/// </summary>
internal sealed class GatewayAdminHarness : IDisposable
{
    /// <summary>The clock's start.</summary>
    public static readonly DateTimeOffset Start = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();

    /// <summary>Opens every store over a fresh directory.</summary>
    /// <param name="config">Configuration keys (<c>Team:Gateway:Bind</c> …) the running settings start from.</param>
    public GatewayAdminHarness(Dictionary<string, string?>? config = null)
    {
        this.Clock = new FakeTimeProvider(Start);
        Dictionary<string, string?> keys = new(config ?? new Dictionary<string, string?>(StringComparer.Ordinal), StringComparer.Ordinal);
        _ = keys.TryAdd("Team:Gateway:StrictLocal", "false");
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(keys).Build();
        this.Running = GatewaySettings.ReadForStartup(this.dir.Path, configuration, new FakeGatewayEnvironment(), this.Clock);
        this.Store = new GatewaySettingsStore(this.dir.Path, this.Clock);
        this.Devices = new DeviceRegistry(this.dir.Path, this.Clock);
        this.Pairing = new PairingService(this.Devices, this.Clock);
        this.Tokens = new BootstrapTokenStore(this.Clock);
        this.Links = new BootstrapLinks(this.Tokens);
        this.Qr = new RecordingQrEncoder();
        this.View = new FakeGatewayRuntimeView();
        this.Service = new GatewayAdminService(this.Store, this.Running, this.Devices, this.Pairing, this.Links, this.Qr, this.View, this.Clock);
    }

    /// <summary>The data directory holding <c>team.db</c>.</summary>
    public string DataDir => this.dir.Path;

    /// <summary>The fake clock.</summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>The running settings, as read at start.</summary>
    public GatewaySettings Running { get; }

    /// <summary>The settings store.</summary>
    public GatewaySettingsStore Store { get; }

    /// <summary>The device registry.</summary>
    public DeviceRegistry Devices { get; }

    /// <summary>The pairing requests.</summary>
    public PairingService Pairing { get; }

    /// <summary>The bootstrap tokens.</summary>
    public BootstrapTokenStore Tokens { get; }

    /// <summary>The setup-link minter.</summary>
    public BootstrapLinks Links { get; }

    /// <summary>The recording QR encoder.</summary>
    public RecordingQrEncoder Qr { get; }

    /// <summary>The runtime view the tests set up.</summary>
    public FakeGatewayRuntimeView View { get; }

    /// <summary>The service under test.</summary>
    public GatewayAdminService Service { get; }

    /// <summary>A principal for a paired device with <paramref name="scopes"/> whose session came in over <paramref name="ingress"/>.</summary>
    /// <param name="deviceId">The device id (the name identifier).</param>
    /// <param name="ingress">The ingress kind as text, for example <c>Loopback</c>.</param>
    /// <param name="scopes">The scopes.</param>
    public static ClaimsPrincipal Principal(string deviceId, string ingress, params string[] scopes) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, deviceId), new Claim(GatewayClaims.Ingress, ingress), .. scopes.Select(scope => new Claim(GatewayClaims.Scope, scope))],
            GatewayClaims.AuthenticationType));

    /// <summary>An admin session.</summary>
    /// <param name="ingress">The ingress kind as text; <c>Loopback</c> by default.</param>
    public static ClaimsPrincipal Admin(string ingress = "Loopback") => Principal("admin-device", ingress, [.. GatewayScopes.AdminSet]);

    /// <summary>A paired session without the admin scope.</summary>
    public static ClaimsPrincipal Limited() => Principal("limited-device", "Direct", GatewayScopes.Read, GatewayScopes.Write);

    /// <summary>Pairs a device and returns its record.</summary>
    /// <param name="name">The device name.</param>
    /// <param name="scopes">The scopes; Limited when none.</param>
    public DeviceRecord PairDevice(string name, params string[] scopes)
    {
        TestDevice device = new();
        _ = this.Devices.Pair(new DeviceRegistry.Registration(
            device.DeviceId, device.PublicKey, name, scopes.Length == 0 ? GatewayScopes.Limited : scopes, "approval", null, "192.168.1.34", "ua/1"));
        return this.Devices.Find(device.DeviceId) ?? throw new InvalidOperationException("The device was not paired.");
    }

    /// <summary>Opens a pairing request and returns it.</summary>
    /// <param name="name">The device name.</param>
    /// <param name="ingress">The ingress kind as text.</param>
    /// <param name="tailscaleLogin">The Tailscale login, if any.</param>
    /// <param name="scopes">The scopes asked for; Limited when none.</param>
    public PairingRequest OpenRequest(string name, string ingress = "Direct", string? tailscaleLogin = null, params string[] scopes)
    {
        TestDevice device = new();
        return this.Pairing.Open(new PairingService.Candidate(
                device.DeviceId, device.PublicKey, scopes.Length == 0 ? GatewayScopes.Limited : scopes, name, "192.168.1.34", "ua/1", ingress, tailscaleLogin))
            ?? throw new InvalidOperationException("The request was not opened.");
    }

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();
}
