using System.Security.Claims;
using System.Text.Json;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Gateway;
using Microsoft.Extensions.Configuration;

namespace Agency.Huddle.Tests;

/// <summary>
/// <see cref="GatewayAdminService"/> (Spec 6.12, 9, Stage E1): every operation refuses without <c>operator.admin</c>, and does what
/// Settings promises with it.
/// </summary>
public sealed class GatewayAdminServiceTests : IDisposable
{
    private readonly GatewayAdminHarness harness = new();
    private readonly ClaimsPrincipal admin = GatewayAdminHarness.Admin();
    private readonly ClaimsPrincipal limited = GatewayAdminHarness.Limited();

    /// <inheritdoc />
    public void Dispose() => this.harness.Dispose();

    private GatewayAdminService Service => this.harness.Service;

    private static GatewayAdminService.Values WithBind(GatewayAdminService.Values source, string bind) => source with { Bind = bind };

    // ---- Fail closed -------------------------------------------------------------------------------------------------------------------

    /// <summary>Reading or saving settings without the admin scope throws, and nothing is stored.</summary>
    [Fact]
    public void Settings_WithoutAdmin_Refuse()
    {
        GatewayAdminService.Values values = this.Service.GetSettings(this.admin).Running;

        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.GetSettings(this.limited));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.SaveSettings(this.limited, WithBind(values, "lan")));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.SaveSettings(new ClaimsPrincipal(new ClaimsIdentity()), WithBind(values, "lan")));
        Assert.Equal("{}", this.harness.Store.StoredOverrides);
    }

    /// <summary>Rotating the token, setting the password and showing the token without the admin scope throw, and the token is unchanged.</summary>
    [Fact]
    public void Secrets_WithoutAdmin_Refuse()
    {
        string before = this.harness.Running.Token;
        ClaimsPrincipal loopbackButLimited = GatewayAdminHarness.Principal("limited-device", "Loopback", GatewayScopes.Read, GatewayScopes.Write);

        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.RotateToken(this.limited));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.SetPassword(this.limited, "a-long-enough-password"));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.ShowToken(loopbackButLimited));
        Assert.Equal(before, this.harness.Running.Token);
        Assert.False(this.harness.Running.HasPassword);
    }

    /// <summary>Listing, scoping, approving and rejecting requests without the admin scope throw, and the request stays pending.</summary>
    [Fact]
    public void Requests_WithoutAdmin_Refuse()
    {
        PairingRequest request = this.harness.OpenRequest("iPad");

        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.PendingRequests(this.limited));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.ScopeMenu(this.limited, request));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.Approve(this.limited, request.RequestId, GatewayScopes.Limited));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.Reject(this.limited, request.RequestId));
        Assert.Equal(PairingService.State.Pending, this.harness.Pairing.StateOf(request.RequestId));
    }

    /// <summary>Listing, renaming, rotating, revoking and removing devices without the admin scope throw, and the device is untouched.</summary>
    [Fact]
    public void Devices_WithoutAdmin_Refuse()
    {
        DeviceRecord device = this.harness.PairDevice("iPhone");

        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.Devices(this.limited));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.Rename(this.limited, device.DeviceId, "Hacked"));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.Rotate(this.limited, device.DeviceId));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.Revoke(this.limited, device.DeviceId));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.Remove(this.limited, device.DeviceId));
        DeviceRecord? after = this.harness.Devices.Find(device.DeviceId);
        Assert.NotNull(after);
        Assert.Equal("iPhone", after.Name);
        Assert.True(after.HasToken);
    }

    /// <summary>Minting a setup link and running the Security check without the admin scope throw, and no token is minted.</summary>
    [Fact]
    public void LinkAndSecurityCheck_WithoutAdmin_Refuse()
    {
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.MintSetupLink(this.limited));
        _ = Assert.Throws<UnauthorizedAccessException>(() => this.Service.SecurityCheck(this.limited));
        Assert.Empty(this.harness.Qr.Payloads);
    }

    // ---- Settings ----------------------------------------------------------------------------------------------------------------------

    /// <summary>Saving stores the overrides as JSON keyed by option path, under <c>gateway_settings.overrides</c>.</summary>
    [Fact]
    public void SaveSettings_StoresOverridesAsJsonKeyedByOptionPath()
    {
        GatewayAdminService.Values running = this.Service.GetSettings(this.admin).Running;

        _ = this.Service.SaveSettings(this.admin, running with { Bind = "lan", AuthMode = "password", AllowedOrigins = ["http://192.168.1.20:5101"] });

        using JsonDocument json = JsonDocument.Parse(this.harness.Store.StoredOverrides);
        Assert.Equal("lan", json.RootElement.GetProperty("Bind").GetString());
        Assert.Equal("password", json.RootElement.GetProperty("Auth:Mode").GetString());
        Assert.Equal("http://192.168.1.20:5101", json.RootElement.GetProperty("AllowedOrigins")[0].GetString());
    }

    /// <summary>Bind and Auth mode wait for the next start; origins apply at once (Spec 9).</summary>
    [Fact]
    public void SaveSettings_SplitsChangesIntoAppliedNowAndNextStart()
    {
        GatewayAdminService.Values running = this.Service.GetSettings(this.admin).Running;

        GatewayAdminService.SaveResult result = this.Service.SaveSettings(
            this.admin,
            running with { Bind = "lan", AuthMode = "password", AllowedOrigins = ["http://192.168.1.20:5101"], PublicOrigin = "https://huddle.example.ts.net" });

        Assert.Equal(["Allowed origins", "Public origin"], result.AppliedNow);
        Assert.Equal(["Bind", "Auth mode"], result.NextStart);
    }

    /// <summary>An applied-now setting changes the running options immediately; a next-start one leaves them as they were.</summary>
    [Fact]
    public void SaveSettings_AppliesOriginsAtOnceAndLeavesBindRunning()
    {
        GatewayAdminService.Values running = this.Service.GetSettings(this.admin).Running;

        _ = this.Service.SaveSettings(this.admin, running with { Bind = "lan", AllowedOrigins = ["http://192.168.1.20:5101"], AllowTailscale = false });

        Assert.Equal(["http://192.168.1.20:5101"], this.harness.Running.Options.AllowedOrigins);
        Assert.False(this.harness.Running.Options.Auth.AllowTailscale);
        Assert.Equal("loopback", this.harness.Running.Options.Bind);
    }

    /// <summary>The settings view shows stored and running side by side and lists the next-start differences as running to stored.</summary>
    [Fact]
    public void GetSettings_AfterSave_ListsPendingRestartChanges()
    {
        GatewayAdminService.Values running = this.Service.GetSettings(this.admin).Running;
        _ = this.Service.SaveSettings(this.admin, running with { Bind = "lan" });

        GatewayAdminService.SettingsView view = this.Service.GetSettings(this.admin);

        Assert.Equal("loopback", view.Running.Bind);
        Assert.Equal("lan", view.Stored.Bind);
        GatewayAdminService.PendingChange pending = Assert.Single(view.Pending);
        Assert.Equal(new GatewayAdminService.PendingChange("Bind", "loopback", "lan"), pending);
    }

    /// <summary>With nothing saved, stored equals running and nothing is pending.</summary>
    [Fact]
    public void GetSettings_NothingSaved_HasNoPendingChanges()
    {
        GatewayAdminService.SettingsView view = this.Service.GetSettings(this.admin);

        Assert.Equal(view.Running, view.Stored);
        Assert.Empty(view.Pending);
    }

    /// <summary>The saved overrides are what the next start reads: <see cref="GatewaySettings.ReadForStartup"/> applies them over configuration.</summary>
    [Fact]
    public void SaveSettings_ThenReadForStartup_ReadsTheOverrides()
    {
        GatewayAdminService.Values running = this.Service.GetSettings(this.admin).Running;
        _ = this.Service.SaveSettings(this.admin, running with { Bind = "tailnet", TailscaleMode = "serve", TailscaleHttpsPort = 5101 });

        GatewaySettings next = GatewaySettings.ReadForStartup(this.harness.DataDir, new ConfigurationBuilder().AddInMemoryCollection([new("Team:Gateway:StrictLocal", "false")]).Build(), new FakeGatewayEnvironment());

        Assert.Equal("tailnet", next.Options.Bind);
        Assert.Equal("serve", next.Options.Tailscale.Mode);
        Assert.Equal(5101, next.Options.Tailscale.HttpsPort);
    }

    // ---- Token and password ------------------------------------------------------------------------------------------------------------

    /// <summary>Rotating replaces the stored and the running token at once; the old one stops verifying.</summary>
    [Fact]
    public void RotateToken_ReplacesStoredAndRunningToken()
    {
        string old = this.harness.Running.Token;

        this.Service.RotateToken(this.admin);

        Assert.NotEqual(old, this.harness.Running.Token);
        Assert.Equal(this.harness.Store.StoredToken, this.harness.Running.Token);
        Assert.False(this.harness.Running.VerifyToken(old));
        Assert.True(this.harness.Running.VerifyToken(this.harness.Running.Token));
    }

    /// <summary>Setting a password makes it verify, stores a hash that is not the password, and keeps the token.</summary>
    [Fact]
    public void SetPassword_StoresAHashAndVerifies()
    {
        string token = this.harness.Running.Token;

        this.Service.SetPassword(this.admin, "correct horse battery");

        Assert.True(this.harness.Running.VerifyPassword("correct horse battery"));
        Assert.False(this.harness.Running.VerifyPassword("wrong"));
        Assert.NotNull(this.harness.Store.StoredPasswordHash);
        Assert.DoesNotContain("correct horse battery", Convert.ToHexString(this.harness.Store.StoredPasswordHash), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(token, this.harness.Running.Token);
    }

    /// <summary>A whitespace password is refused.</summary>
    [Fact]
    public void SetPassword_Blank_Throws() =>
        _ = Assert.Throws<ArgumentException>(() => this.Service.SetPassword(this.admin, "   "));

    /// <summary>The token is shown to a loopback admin session.</summary>
    [Fact]
    public void ShowToken_LoopbackAdmin_ReturnsTheToken() =>
        Assert.Equal(this.harness.Running.Token, this.Service.ShowToken(GatewayAdminHarness.Admin("Loopback")));

    /// <summary>The token is never shown to an admin session that came in over the network, whatever the ingress.</summary>
    [Theory]
    [InlineData("Direct")]
    [InlineData("Managed")]
    [InlineData("TrustedProxy")]
    public void ShowToken_NonLoopbackAdmin_ReturnsNull(string ingress) =>
        Assert.Null(this.Service.ShowToken(GatewayAdminHarness.Admin(ingress)));

    // ---- Requests ----------------------------------------------------------------------------------------------------------------------

    /// <summary>The pending list is the pairing service's.</summary>
    [Fact]
    public void PendingRequests_ListsOpenRequests()
    {
        PairingRequest request = this.harness.OpenRequest("iPad", "Direct");

        Assert.Equal(request.RequestId, Assert.Single(this.Service.PendingRequests(this.admin)).RequestId);
    }

    /// <summary>A Limited request's menu offers Limited only; an admin request's offers Limited and Admin.</summary>
    [Fact]
    public void ScopeMenu_OffersAdminOnlyForAnAdminRequest()
    {
        PairingRequest limitedRequest = this.harness.OpenRequest("iPad");
        PairingRequest adminRequest = this.harness.OpenRequest("Laptop", scopes: [GatewayScopes.Admin]);

        Assert.Equal(["Limited"], this.Service.ScopeMenu(this.admin, limitedRequest).Select(choice => choice.Label).ToList());
        Assert.Equal(["Limited", "Admin"], this.Service.ScopeMenu(this.admin, adminRequest).Select(choice => choice.Label).ToList());
        Assert.Equal(GatewayScopes.Limited, this.Service.ScopeMenu(this.admin, limitedRequest)[0].Scopes);
    }

    /// <summary>Approving with the chosen scopes pairs the device with them.</summary>
    [Fact]
    public void Approve_PairsTheDeviceWithTheChosenScopes()
    {
        PairingRequest request = this.harness.OpenRequest("iPad");

        PairingService.ApprovalResult result = this.Service.Approve(this.admin, request.RequestId, GatewayScopes.Limited);

        Assert.Equal(PairingService.ApprovalOutcome.Approved, result.Outcome);
        Assert.NotNull(this.harness.Devices.Find(request.DeviceId));
    }

    /// <summary>Rejecting ends the request without pairing anything.</summary>
    [Fact]
    public void Reject_EndsTheRequestWithoutPairing()
    {
        PairingRequest request = this.harness.OpenRequest("iPad");

        Assert.True(this.Service.Reject(this.admin, request.RequestId));

        Assert.Equal(PairingService.State.Rejected, this.harness.Pairing.StateOf(request.RequestId));
        Assert.Null(this.harness.Devices.Find(request.DeviceId));
    }

    // ---- Devices -----------------------------------------------------------------------------------------------------------------------

    /// <summary>The device list is the registry's.</summary>
    [Fact]
    public void Devices_ListsPairedDevices()
    {
        DeviceRecord device = this.harness.PairDevice("iPhone");

        Assert.Equal(device.DeviceId, Assert.Single(this.Service.Devices(this.admin)).DeviceId);
    }

    /// <summary>Renaming changes the stored name.</summary>
    [Fact]
    public void Rename_ChangesTheName()
    {
        DeviceRecord device = this.harness.PairDevice("iPhone");

        Assert.True(this.Service.Rename(this.admin, device.DeviceId, "Ada's phone"));

        Assert.Equal("Ada's phone", this.harness.Devices.Find(device.DeviceId)?.Name);
    }

    /// <summary>Revoking drops the device token but keeps the device in the list.</summary>
    [Fact]
    public void Revoke_DropsTheTokenButKeepsTheDevice()
    {
        DeviceRecord device = this.harness.PairDevice("iPhone");

        Assert.True(this.Service.Revoke(this.admin, device.DeviceId));

        Assert.False(this.harness.Devices.Find(device.DeviceId)?.HasToken);
    }

    /// <summary>Removing deletes the device.</summary>
    [Fact]
    public void Remove_DeletesTheDevice()
    {
        DeviceRecord device = this.harness.PairDevice("iPhone");

        Assert.True(this.Service.Remove(this.admin, device.DeviceId));

        Assert.Null(this.harness.Devices.Find(device.DeviceId));
    }

    /// <summary>Rotating gives the device a new token; an unknown device answers false.</summary>
    [Fact]
    public void Rotate_GivesAKnownDeviceANewTokenAndAnUnknownOneFalse()
    {
        DeviceRecord device = this.harness.PairDevice("iPhone");

        Assert.True(this.Service.Rotate(this.admin, device.DeviceId));
        Assert.False(this.Service.Rotate(this.admin, "unknown"));
    }

    // ---- Setup link and QR -------------------------------------------------------------------------------------------------------------

    /// <summary>A setup link is a limited bootstrap link for the public address, 10 minutes long, with the QR of exactly that link.</summary>
    [Fact]
    public void MintSetupLink_ReturnsTheLinkAndAQrOfExactlyThatLink()
    {
        GatewayAdminService.SetupLinkOffer offer = this.Service.MintSetupLink(this.admin);

        Assert.True(offer.Allowed);
        Assert.NotNull(offer.Url);
        Assert.StartsWith("https://desktop-a.tail1234.ts.net:5101" + BootstrapLinks.Suffix, offer.Url, StringComparison.Ordinal);
        Assert.Equal(offer.Url, Assert.Single(this.harness.Qr.Payloads));
        Assert.Equal("data:image/png;base64," + Convert.ToBase64String(RecordingQrEncoder.Png), offer.QrDataUri);
        Assert.Equal(GatewayAdminHarness.Start + BootstrapTokenStore.Lifetime, offer.ExpiresAt);
    }

    /// <summary>The token in the link redeems as a limited setup link, never as admin.</summary>
    [Fact]
    public void MintSetupLink_TokenIsLimited()
    {
        GatewayAdminService.SetupLinkOffer offer = this.Service.MintSetupLink(this.admin);
        string url = offer.Url ?? string.Empty;
        string token = url[(url.IndexOf("bootstrapToken=", StringComparison.Ordinal) + "bootstrapToken=".Length)..];

        BootstrapTokenStore.Redemption redemption = this.harness.Tokens.Redeem(token, "some-device");

        Assert.True(redemption.Valid);
        Assert.Equal(BootstrapTokenStore.Kind.SetupLink, redemption.TokenKind);
        Assert.DoesNotContain(GatewayScopes.Admin, redemption.Scopes);
    }

    /// <summary>No public address means no link and no QR.</summary>
    [Fact]
    public void MintSetupLink_WithoutAnAddress_IsNotAllowed()
    {
        this.harness.View.PublicUrl = null;

        GatewayAdminService.SetupLinkOffer offer = this.Service.MintSetupLink(this.admin);

        Assert.False(offer.Allowed);
        Assert.Null(offer.Url);
        Assert.Null(offer.QrDataUri);
        Assert.Empty(this.harness.Qr.Payloads);
    }

    /// <summary>A plain-http address that is not private is refused with the reason and never reaches the encoder.</summary>
    [Fact]
    public void MintSetupLink_PlainHttpToAPublicHost_IsRefusedWithoutAQr()
    {
        this.harness.View.PublicUrl = "http://example.com:5101";

        GatewayAdminService.SetupLinkOffer offer = this.Service.MintSetupLink(this.admin);

        Assert.False(offer.Allowed);
        Assert.NotNull(offer.Reason);
        Assert.Null(offer.QrDataUri);
        Assert.Empty(this.harness.Qr.Payloads);
    }

    /// <summary>The QRCoder encoder produces a PNG (the 8-byte PNG signature).</summary>
    [Fact]
    public void QrCoderPngEncoder_ProducesAPng()
    {
        byte[] png = new QrCoderPngEncoder().EncodePng("https://desktop-a.tail1234.ts.net:5101/auth#bootstrapToken=abc");

        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png.Take(8).ToArray());
    }

    // ---- Security check ----------------------------------------------------------------------------------------------------------------

    /// <summary>The service feeds the running options, the token length and the runtime view into the check.</summary>
    [Fact]
    public void SecurityCheck_UsesTheRunningGatewayAndTheRuntimeView()
    {
        this.harness.Running.Options.Bind = "lan";
        this.harness.View.StrayRoutes = [new TailscaleServeStatus.Route(443, "/", "http://127.0.0.1:41233")];

        IReadOnlyList<GatewaySecurityCheck.Item> items = this.Service.SecurityCheck(this.admin);

        Assert.Contains(items, item => item.Id == "gateway.downgraded_transport");
        Assert.Contains(items, item => item.Id == "gateway.stray_serve_route");
    }
}
