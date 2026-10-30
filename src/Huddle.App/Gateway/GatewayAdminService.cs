using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// Every change Settings makes to the gateway (Spec 6.12). Each operation takes the caller's <see cref="ClaimsPrincipal"/> and refuses
/// with <see cref="UnauthorizedAccessException"/> unless it holds <see cref="GatewayScopes.Admin"/>.
/// </summary>
internal sealed class GatewayAdminService
{
    private static readonly string[] Binds = ["loopback", "lan", "tailnet", "auto", "custom"];
    private static readonly string[] AuthModes = ["none", "token", "password", "trusted-proxy"];
    private static readonly string[] TailscaleModes = ["off", "serve", "funnel"];

    private static readonly Setting[] Settings =
    [
        new("Bind", GatewayOverrides.Bind, false, static v => v.Bind, static v => JsonValue.Create(v.Bind)),
        new("Auth mode", GatewayOverrides.AuthMode, false, static v => v.AuthMode, static v => JsonValue.Create(v.AuthMode)),
        new("Port", GatewayOverrides.Port, false, static v => NumberText(v.Port), static v => JsonValue.Create(v.Port)),
        new("Custom bind host", GatewayOverrides.CustomBindHost, false, static v => v.CustomBindHost ?? "none", static v => JsonValue.Create(v.CustomBindHost)),
        new("Tailscale mode", GatewayOverrides.TailscaleMode, false, static v => v.TailscaleMode, static v => JsonValue.Create(v.TailscaleMode)),
        new("Tailscale HTTPS port", GatewayOverrides.TailscaleHttpsPort, false, static v => NumberText(v.TailscaleHttpsPort), static v => JsonValue.Create(v.TailscaleHttpsPort)),
        new("Use Tailscale sign-in", GatewayOverrides.AllowTailscale, true, static v => v.AllowTailscale ? "on" : "off", static v => JsonValue.Create(v.AllowTailscale)),
        new("Trusted proxies", GatewayOverrides.TrustedProxies, true, static v => string.Join(", ", v.TrustedProxies), static v => ListOf(v.TrustedProxies)),
        new("Allowed origins", GatewayOverrides.AllowedOrigins, true, static v => string.Join(", ", v.AllowedOrigins), static v => ListOf(v.AllowedOrigins)),
        new("Public origin", GatewayOverrides.PublicOrigin, true, static v => v.PublicOrigin ?? "none", static v => JsonValue.Create(v.PublicOrigin)),
    ];

    private readonly GatewaySettingsStore store;
    private readonly GatewaySettings running;
    private readonly DeviceRegistry devices;
    private readonly PairingService pairing;
    private readonly BootstrapLinks links;
    private readonly IQrPngEncoder qr;
    private readonly IGatewayRuntimeView view;
    private readonly TimeProvider timeProvider;
    private readonly Lock gate = new();

    /// <summary>Creates the service over the gateway's parts.</summary>
    /// <param name="store">Where overrides, token and password are stored.</param>
    /// <param name="running">The running settings.</param>
    /// <param name="devices">The device registry.</param>
    /// <param name="pairing">The pairing requests.</param>
    /// <param name="links">The setup-link minter.</param>
    /// <param name="qr">The QR encoder.</param>
    /// <param name="view">What the running gateway looks like.</param>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    public GatewayAdminService(
        GatewaySettingsStore store,
        GatewaySettings running,
        DeviceRegistry devices,
        PairingService pairing,
        BootstrapLinks links,
        IQrPngEncoder qr,
        IGatewayRuntimeView view,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(running);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(pairing);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(qr);
        ArgumentNullException.ThrowIfNull(view);

        this.store = store;
        this.running = running;
        this.devices = devices;
        this.pairing = pairing;
        this.links = links;
        this.qr = qr;
        this.view = view;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The editable gateway settings, as stored or as running.</summary>
    /// <param name="Bind"><c>loopback</c>, <c>lan</c>, <c>tailnet</c> or <c>custom</c>.</param>
    /// <param name="AuthMode"><c>none</c>, <c>token</c>, <c>password</c> or <c>trusted-proxy</c>.</param>
    /// <param name="Port">The port, or <see langword="null"/> for the default.</param>
    /// <param name="CustomBindHost">The host of a custom bind.</param>
    /// <param name="TailscaleMode"><c>off</c>, <c>serve</c> or <c>funnel</c>.</param>
    /// <param name="TailscaleHttpsPort">The Tailscale HTTPS port.</param>
    /// <param name="AllowTailscale">Whether Tailscale sign-in is accepted.</param>
    /// <param name="TrustedProxies">The trusted proxy addresses.</param>
    /// <param name="AllowedOrigins">The allowed origins.</param>
    /// <param name="PublicOrigin">The public origin.</param>
    internal sealed record Values(
        string Bind,
        string AuthMode,
        int? Port,
        string? CustomBindHost,
        string TailscaleMode,
        int? TailscaleHttpsPort,
        bool AllowTailscale,
        IReadOnlyList<string> TrustedProxies,
        IReadOnlyList<string> AllowedOrigins,
        string? PublicOrigin)
    {
        /// <summary>Equal when every setting is equal; the two lists compare by their items.</summary>
        /// <param name="other">The other values.</param>
        public bool Equals(Values? other) =>
            other is not null
            && string.Equals(this.Bind, other.Bind, StringComparison.Ordinal)
            && string.Equals(this.AuthMode, other.AuthMode, StringComparison.Ordinal)
            && this.Port == other.Port
            && string.Equals(this.CustomBindHost, other.CustomBindHost, StringComparison.Ordinal)
            && string.Equals(this.TailscaleMode, other.TailscaleMode, StringComparison.Ordinal)
            && this.TailscaleHttpsPort == other.TailscaleHttpsPort
            && this.AllowTailscale == other.AllowTailscale
            && this.TrustedProxies.SequenceEqual(other.TrustedProxies, StringComparer.Ordinal)
            && this.AllowedOrigins.SequenceEqual(other.AllowedOrigins, StringComparer.Ordinal)
            && string.Equals(this.PublicOrigin, other.PublicOrigin, StringComparison.Ordinal);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(this.Bind, this.AuthMode, this.Port, this.TailscaleMode, this.AllowTailscale, this.AllowedOrigins.Count, this.PublicOrigin);
    }

    /// <summary>A setting whose stored value differs from the running one and waits for a restart.</summary>
    /// <param name="Setting">The display name, for example <c>Bind</c> or <c>Auth mode</c>.</param>
    /// <param name="Running">The value running now.</param>
    /// <param name="Stored">The value that applies at the next start.</param>
    internal sealed record PendingChange(string Setting, string Running, string Stored);

    /// <summary>The stored and running values side by side.</summary>
    /// <param name="Running">What is running.</param>
    /// <param name="Stored">What is stored (overrides over configuration).</param>
    /// <param name="Pending">The next-start settings that differ.</param>
    internal sealed record SettingsView(Values Running, Values Stored, IReadOnlyList<PendingChange> Pending);

    /// <summary>The outcome of a save, by display name.</summary>
    /// <param name="AppliedNow">Changed settings that apply at once.</param>
    /// <param name="NextStart">Changed settings that apply at the next start.</param>
    internal sealed record SaveResult(IReadOnlyList<string> AppliedNow, IReadOnlyList<string> NextStart);

    /// <summary>One entry of the Approve menu.</summary>
    /// <param name="Label"><c>Limited</c> or <c>Admin</c>.</param>
    /// <param name="Scopes">The scopes it grants.</param>
    internal sealed record ScopeChoice(string Label, IReadOnlyList<string> Scopes);

    /// <summary>A setup link ready to show.</summary>
    /// <param name="Allowed">Whether a link could be made.</param>
    /// <param name="Url">The link.</param>
    /// <param name="QrDataUri">The QR code as <c>data:image/png;base64,…</c>.</param>
    /// <param name="Reason">Why not, when not allowed.</param>
    /// <param name="ExpiresAt">When the link stops working.</param>
    internal sealed record SetupLinkOffer(bool Allowed, string? Url, string? QrDataUri, string? Reason, DateTimeOffset ExpiresAt);

    /// <summary>One editable setting: its display name, override key, when it applies, and how to show and serialise it.</summary>
    private sealed record Setting(string Name, string Key, bool AppliesNow, Func<Values, string> Show, Func<Values, JsonNode?> Json);

    /// <summary>The stored and running settings, and what waits for a restart.</summary>
    /// <param name="caller">The caller.</param>
    public SettingsView GetSettings(ClaimsPrincipal caller)
    {
        RequireAdmin(caller);

        Values now = ReadValues(this.running.Options);
        Values stored = this.StoredValues();
        List<PendingChange> pending = [];
        foreach (Setting setting in Settings.Where(static setting => !setting.AppliesNow))
        {
            if (!Same(setting, now, stored))
            {
                pending.Add(new PendingChange(setting.Name, setting.Show(now), setting.Show(stored)));
            }
        }

        return new SettingsView(now, stored, pending);
    }

    /// <summary>Stores <paramref name="values"/> as overrides; the "apply now" ones also change the running settings.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="values">The full set of settings.</param>
    public SaveResult SaveSettings(ClaimsPrincipal caller, Values values)
    {
        RequireAdmin(caller);
        Validate(values);
        values = values with { Bind = values.Bind.ToLowerInvariant(), AuthMode = values.AuthMode.ToLowerInvariant(), TailscaleMode = values.TailscaleMode.ToLowerInvariant() };

        lock (this.gate)
        {
            Values stored = this.StoredValues();
            List<Setting> changed = [.. Settings.Where(setting => !Same(setting, values, stored))];
            JsonObject overrides = ParseObject(this.store.StoredOverrides);
            JsonObject applyNow = [];
            foreach (Setting setting in changed)
            {
                overrides[setting.Key] = setting.Json(values);
                if (setting.AppliesNow)
                {
                    applyNow[setting.Key] = setting.Json(values);
                }
            }

            if (changed.Count > 0)
            {
                this.store.SetOverrides(overrides.ToJsonString());
                GatewayOverrides.Apply(this.running.Options, applyNow.ToJsonString());
            }

            return new SaveResult(
                [.. changed.Where(static setting => setting.AppliesNow).Select(static setting => setting.Name)],
                [.. changed.Where(static setting => !setting.AppliesNow).Select(static setting => setting.Name)]);
        }
    }

    /// <summary>Replaces the stored token; the running token changes at once and sessions stay.</summary>
    /// <param name="caller">The caller.</param>
    public void RotateToken(ClaimsPrincipal caller)
    {
        RequireAdmin(caller);

        this.running.ReplaceToken(this.store.RotateToken());
    }

    /// <summary>Stores a PBKDF2 hash of <paramref name="password"/>; nothing ever returns it.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="password">The new password.</param>
    public void SetPassword(ClaimsPrincipal caller, string password)
    {
        RequireAdmin(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        this.store.SetPassword(password);
        if (this.store.StoredPasswordHash is { } hash)
        {
            this.running.ReplacePasswordHash(hash);
        }
    }

    /// <summary>The running token, or <see langword="null"/> unless the caller's session came in over loopback.</summary>
    /// <param name="caller">The caller.</param>
    public string? ShowToken(ClaimsPrincipal caller)
    {
        RequireAdmin(caller);

        return caller.HasClaim(GatewayClaims.Ingress, IngressKindText.Of(IngressKind.Loopback)) ? this.running.Token : null;
    }

    /// <summary>The pairing and admin requests waiting for approval.</summary>
    /// <param name="caller">The caller.</param>
    public IReadOnlyList<PairingRequest> PendingRequests(ClaimsPrincipal caller)
    {
        RequireAdmin(caller);

        return this.pairing.Pending();
    }

    /// <summary>The Approve menu for <paramref name="request"/>: Limited, and Admin only for an admin request the caller may grant.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="request">The request.</param>
    public IReadOnlyList<ScopeChoice> ScopeMenu(ClaimsPrincipal caller, PairingRequest request)
    {
        RequireAdmin(caller);
        ArgumentNullException.ThrowIfNull(request);

        if (this.pairing.StateOf(request.RequestId) != PairingService.State.Pending)
        {
            return [];
        }

        List<ScopeChoice> menu = [new ScopeChoice("Limited", GatewayScopes.Limited)];
        if (request.IsAdminRequest)
        {
            menu.Add(new ScopeChoice("Admin", GatewayScopes.AdminSet));
        }

        return menu;
    }

    /// <summary>Approves a request with the scopes chosen, within the caller's own.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="granted">The scopes chosen.</param>
    public PairingService.ApprovalResult Approve(ClaimsPrincipal caller, string requestId, IReadOnlyList<string> granted)
    {
        RequireAdmin(caller);

        return this.pairing.Approve(requestId, ScopesOf(caller), granted);
    }

    /// <summary>Rejects a request.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="requestId">The request id.</param>
    public bool Reject(ClaimsPrincipal caller, string requestId)
    {
        RequireAdmin(caller);

        return this.pairing.Reject(requestId, ScopesOf(caller));
    }

    /// <summary>The paired devices.</summary>
    /// <param name="caller">The caller.</param>
    public IReadOnlyList<DeviceRecord> Devices(ClaimsPrincipal caller)
    {
        RequireAdmin(caller);

        return this.devices.List();
    }

    /// <summary>Renames a device.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="name">The new name.</param>
    public bool Rename(ClaimsPrincipal caller, string deviceId, string name)
    {
        RequireAdmin(caller);

        return this.devices.Rename(deviceId, name);
    }

    /// <summary>Rotates a device's token.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="deviceId">The device.</param>
    public bool Rotate(ClaimsPrincipal caller, string deviceId)
    {
        RequireAdmin(caller);

        return this.pairing.Rotate(deviceId) is not null;
    }

    /// <summary>Revokes a device's token and ends its sessions.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="deviceId">The device.</param>
    public bool Revoke(ClaimsPrincipal caller, string deviceId)
    {
        RequireAdmin(caller);

        return this.devices.Revoke(deviceId);
    }

    /// <summary>Removes a device.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="deviceId">The device.</param>
    public bool Remove(ClaimsPrincipal caller, string deviceId)
    {
        RequireAdmin(caller);

        return this.devices.Remove(deviceId);
    }

    /// <summary>Whether a setup link shown earlier can no longer be used: it was redeemed, burned or has expired.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="url">The link's address.</param>
    public bool IsSetupLinkSpent(ClaimsPrincipal caller, string url)
    {
        RequireAdmin(caller);

        return this.links.IsSpent(url);
    }

    /// <summary>Mints a limited setup link for the gateway's public address, with its QR code.</summary>
    /// <param name="caller">The caller.</param>
    public SetupLinkOffer MintSetupLink(ClaimsPrincipal caller)
    {
        RequireAdmin(caller);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        if (this.view.PublicUrl is not { Length: > 0 } origin)
        {
            return new SetupLinkOffer(false, null, null, "There is no address to pair against yet. Turn on Tailscale, or set a LAN bind and a public origin.", now);
        }

        BootstrapLinks.SetupLinkResult link = this.links.SetupLink(origin);
        if (!link.Allowed || link.Url is null)
        {
            return new SetupLinkOffer(false, null, null, link.Reason, now);
        }

        string dataUri = "data:image/png;base64," + Convert.ToBase64String(this.qr.EncodePng(link.Url));
        return new SetupLinkOffer(true, link.Url, dataUri, null, now + BootstrapTokenStore.Lifetime);
    }

    /// <summary>The Security check for the running gateway.</summary>
    /// <param name="caller">The caller.</param>
    public IReadOnlyList<GatewaySecurityCheck.Item> SecurityCheck(ClaimsPrincipal caller)
    {
        RequireAdmin(caller);

        return GatewaySecurityCheck.Evaluate(new GatewaySecurityCheck.Input(
            this.running.Options,
            this.running.Token.Length,
            this.running.KnownPasswordLength,
            this.view.TailscaleState,
            this.view.BareNameHostInUse,
            this.view.StrayRoutes));
    }

    private static void RequireAdmin(ClaimsPrincipal caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.HasClaim(GatewayClaims.Scope, GatewayScopes.Admin))
        {
            throw new UnauthorizedAccessException("Admin access is required.");
        }
    }

    private static string[] ScopesOf(ClaimsPrincipal caller) => [.. caller.FindAll(GatewayClaims.Scope).Select(static claim => claim.Value)];

    private static Values ReadValues(GatewayOptions options) => new(
        options.Bind.ToLowerInvariant(),
        options.Auth.ModeText.ToLowerInvariant(),
        options.Port,
        options.CustomBindHost,
        options.Tailscale.Mode.ToLowerInvariant(),
        options.Tailscale.HttpsPort,
        options.Auth.AllowTailscale,
        [.. options.TrustedProxies],
        [.. options.AllowedOrigins],
        options.PublicOrigin);

    private static bool Same(Setting setting, Values left, Values right) =>
        string.Equals(setting.Json(left)?.ToJsonString(), setting.Json(right)?.ToJsonString(), StringComparison.Ordinal);

    private static string NumberText(int? number) => number is { } value ? value.ToString(CultureInfo.InvariantCulture) : "default";

    private static JsonArray ListOf(IReadOnlyList<string> items) => [.. items.Select(static item => JsonValue.Create(item))];

    private static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            // A corrupt cell is replaced by the next save.
            return [];
        }
    }

    private static void Validate(Values values)
    {
        ArgumentNullException.ThrowIfNull(values);

        RequireOneOf(values.Bind, Binds, "Bind");
        RequireOneOf(values.AuthMode, AuthModes, "Auth mode");
        RequireOneOf(values.TailscaleMode, TailscaleModes, "Tailscale mode");
        RequirePort(values.Port, "Port");
        RequirePort(values.TailscaleHttpsPort, "Tailscale HTTPS port");
    }

    private static void RequireOneOf(string value, string[] allowed, string name)
    {
        if (!allowed.Contains(value?.ToLowerInvariant(), StringComparer.Ordinal))
        {
            throw new ArgumentException($"{name} must be one of: {string.Join(", ", allowed)}.", nameof(value));
        }
    }

    private static void RequirePort(int? port, string name)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentException($"{name} must be a number from 1 to 65535.", nameof(port));
        }
    }

    private Values StoredValues()
    {
        GatewayOptions copy = GatewayOverrides.Copy(this.running.Options);
        GatewayOverrides.Apply(copy, this.store.StoredOverrides);
        return ReadValues(copy);
    }
}
