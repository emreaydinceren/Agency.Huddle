using Agency.Huddle.App.Components.Settings.RemoteAccess;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Gateway;
using Agency.Huddle.Tests.Ui;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Settings.RemoteAccess;

/// <summary>
/// <see cref="RemoteAccessPanel"/> (Spec 6.12, Appendix B, Stage E1): each part of wireframe G10, with every user-facing string pinned.
/// The panel marks its parts with <c>data-testid</c> (<c>gateway</c>, <c>requests</c>, <c>pair</c>, <c>devices</c>, <c>security</c>) and
/// <c>data-part</c> inside a row, so a test finds a part without depending on MudBlazor's markup.
/// </summary>
public sealed class RemoteAccessPanelTests : IAsyncDisposable
{
    private readonly MudBunitContext ctx = new();
    private GatewayAdminHarness? harness;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.ctx.DisposeAsync();
        this.harness?.Dispose();
    }

    private GatewayAdminHarness H => this.harness ?? this.Start();

    /// <summary>Opens the harness (once) and registers what the panel injects.</summary>
    private GatewayAdminHarness Start(Dictionary<string, string?>? config = null)
    {
        this.harness = new GatewayAdminHarness(config);
        this.ctx.Services.AddSingleton(this.harness.Service);
        this.ctx.Services.AddSingleton<IGatewayRuntimeView>(this.harness.View);
        this.ctx.Services.AddSingleton<TimeProvider>(this.harness.Clock);
        return this.harness;
    }

    private IRenderedComponent<ContainerFragment> Render()
    {
        _ = this.H;
        return this.ctx.RenderWithPopovers<RemoteAccessPanel>();
    }

    private static IElement Part(IRenderedComponent<ContainerFragment> cut, string testId) => cut.Find($"[data-testid={testId}]");

    private static IElement Button(IElement scope, string text) =>
        scope.QuerySelectorAll("button").Single(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));

    private static string TextOf(IElement scope, string part) => scope.QuerySelector($"[data-part={part}]")?.TextContent.Trim() ?? "<missing>";

    private static IElement DeviceRow(IRenderedComponent<ContainerFragment> cut, string deviceId) =>
        cut.Find($"[data-testid=device-row][data-device-id='{deviceId}']");

    private static IElement RequestRow(IRenderedComponent<ContainerFragment> cut, string requestId) =>
        cut.Find($"[data-testid=request-row][data-request-id='{requestId}']");

    private static async Task Click(IRenderedComponent<ContainerFragment> cut, Func<IElement> find) => await cut.InvokeAsync(() => find().ClickAsync());

    // ---- The panel ---------------------------------------------------------------------------------------------------------------------

    /// <summary>The panel opens with the intro sentence of wireframe G10 and the five parts.</summary>
    [Fact]
    public void Panel_ShowsTheIntroAndItsParts()
    {
        _ = this.Start(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Team:Gateway:StrictLocal"] = "false" });
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Reach Huddle from other devices. Every device needs the gateway secret and your approval.", cut.TextOf(".settings-intro"));
        Assert.NotNull(Part(cut, "gateway"));
        Assert.NotNull(Part(cut, "pair"));
        Assert.NotNull(Part(cut, "devices"));
        Assert.NotNull(Part(cut, "security"));
        Assert.Contains("Gateway", Part(cut, "gateway").TextContent, StringComparison.Ordinal);
        Assert.Contains("Pair a device", Part(cut, "pair").TextContent, StringComparison.Ordinal);
        Assert.Contains("Security check", Part(cut, "security").TextContent, StringComparison.Ordinal);
    }

    // ---- G10a Gateway card -------------------------------------------------------------------------------------------------------------

    /// <summary>The token is drawn masked and its value is nowhere in the page.</summary>
    [Fact]
    public void Gateway_TokenIsMaskedAndNeverInTheMarkup()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("••••••••", cut.TextOf("[data-testid=token]"));
        Assert.DoesNotContain(this.H.Running.Token, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A loopback admin session gets Show, and Show reveals the token.</summary>
    [Fact]
    public async Task Gateway_LoopbackSession_ShowRevealsTheToken()
    {
        this.ctx.SignInWithIngress("Loopback", "operator.read", "operator.write", "operator.pairing", "operator.admin");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(Part(cut, "gateway"), "Show"));

        Assert.Equal(this.H.Running.Token, cut.TextOf("[data-testid=token]"));
    }

    /// <summary>An admin session that came in over the network has no Show button.</summary>
    [Fact]
    public void Gateway_NonLoopbackSession_HasNoShowButton()
    {
        this.ctx.SignInWithIngress("Managed", "operator.read", "operator.write", "operator.pairing", "operator.admin");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.DoesNotContain(Part(cut, "gateway").QuerySelectorAll("button"), button => button.TextContent.Trim() == "Show");
    }

    /// <summary>Rotate in the Gateway card replaces the token and says so.</summary>
    [Fact]
    public async Task Gateway_Rotate_ReplacesTheToken()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();
        string old = this.H.Running.Token;

        await Click(cut, () => Button(Part(cut, "gateway"), "Rotate"));

        Assert.NotEqual(old, this.H.Running.Token);
        Assert.Equal("Token rotated. Devices stay signed in.", cut.TextOf("[data-testid=gateway-notice]"));
    }

    /// <summary>Copy link copies the public address through the clipboard helper.</summary>
    [Fact]
    public async Task Gateway_CopyLink_CopiesThePublicAddress()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(Part(cut, "gateway"), "Copy link"));

        Assert.Equal("https://desktop-a.tail1234.ts.net:5101", cut.TextOf("[data-testid=public-url]"));
        _ = this.ctx.JSInterop.VerifyInvoke("huddleClipboard.copy");
        Assert.Equal("https://desktop-a.tail1234.ts.net:5101", this.ctx.JSInterop.Invocations["huddleClipboard.copy"].Single().Arguments[0]);
    }

    /// <summary>With no address there is no link and no Copy link.</summary>
    [Fact]
    public void Gateway_NoPublicAddress_HasNoCopyLink()
    {
        this.H.View.PublicUrl = null;
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.DoesNotContain(Part(cut, "gateway").QuerySelectorAll("button"), button => button.TextContent.Trim() == "Copy link");
    }

    /// <summary>A healthy listener shows the Running chip.</summary>
    [Fact]
    public void Gateway_Healthy_ShowsRunning()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Running", cut.TextOf("[data-testid=status]"));
    }

    /// <summary>A listener that is not fully up shows Needs attention and the reason.</summary>
    [Fact]
    public void Gateway_ListenerReason_ShowsNeedsAttentionAndTheReason()
    {
        this.H.View.ListenerReason = "The bind was refused because sign-in is off.";
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Needs attention", cut.TextOf("[data-testid=status]"));
        Assert.Equal("The bind was refused because sign-in is off.", cut.TextOf("[data-testid=status-reason]"));
    }

    /// <summary>With Serve chosen and Tailscale signed out, the next step says so.</summary>
    [Fact]
    public void Gateway_TailscaleSignedOut_SaysWhatToDo()
    {
        this.Start(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Team:Gateway:Tailscale:Mode"] = "serve" });
        this.H.View.TailscaleState = TailscaleState.NeedsLogin;
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Tailscale is signed out. Sign in to Tailscale, then restart Huddle.", cut.TextOf("[data-testid=status-next]"));
    }

    /// <summary>With Serve chosen and Tailscale missing, the next step says so.</summary>
    [Fact]
    public void Gateway_TailscaleMissing_SaysWhatToDo()
    {
        this.Start(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Team:Gateway:Tailscale:Mode"] = "serve" });
        this.H.View.TailscaleState = TailscaleState.Missing;
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Tailscale is not installed. Install it from tailscale.com, then restart Huddle.", cut.TextOf("[data-testid=status-next]"));
    }

    /// <summary>Nothing saved means no restart line.</summary>
    [Fact]
    public void Gateway_NothingPending_HasNoRestartLine()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Empty(cut.FindAll("[data-testid=restart-line]"));
    }

    /// <summary>A saved next-start change shows the line "Restart Huddle to apply: Bind running → stored".</summary>
    [Fact]
    public void Gateway_PendingBind_ShowsTheRestartLine()
    {
        GatewayAdminService.Values running = this.H.Service.GetSettings(GatewayAdminHarness.Admin()).Running;
        _ = this.H.Service.SaveSettings(GatewayAdminHarness.Admin(), running with { Bind = "lan" });
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Restart Huddle to apply: Bind loopback → lan", cut.TextOf("[data-testid=restart-line]"));
    }

    /// <summary>Several pending changes are joined with commas, in the order Bind, Auth mode.</summary>
    [Fact]
    public void Gateway_SeveralPending_AreJoinedWithCommas()
    {
        GatewayAdminService.Values running = this.H.Service.GetSettings(GatewayAdminHarness.Admin()).Running;
        _ = this.H.Service.SaveSettings(GatewayAdminHarness.Admin(), running with { Bind = "lan", AuthMode = "password" });
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Restart Huddle to apply: Bind loopback → lan, Auth mode token → password", cut.TextOf("[data-testid=restart-line]"));
    }

    /// <summary>Stored and running values are both drawn: the control shows the stored value and the running one is labelled.</summary>
    [Fact]
    public void Gateway_PendingBind_ShowsStoredAndRunningSideBySide()
    {
        GatewayAdminService.Values running = this.H.Service.GetSettings(GatewayAdminHarness.Admin()).Running;
        _ = this.H.Service.SaveSettings(GatewayAdminHarness.Admin(), running with { Bind = "lan" });
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Running: loopback", cut.TextOf("[data-testid=running-bind]"));
        Assert.Equal("lan", cut.Find("[data-testid=stored-bind] input").GetAttribute("value"));
    }

    /// <summary>Saving a change that applies now says it applies now and changes the running options.</summary>
    [Fact]
    public async Task Gateway_SaveAnApplyNowChange_SaysItAppliesNow()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await cut.InvokeAsync(() => cut.Find("[data-testid=allow-tailscale] input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false }));
        await Click(cut, () => Button(Part(cut, "gateway"), "Save"));

        Assert.False(this.H.Running.Options.Auth.AllowTailscale);
        Assert.Equal("Saved. Applies now: Use Tailscale sign-in", cut.TextOf("[data-testid=save-result]"));
    }

    /// <summary>Setting a password stores it, says so, clears the field and never draws the password.</summary>
    [Fact]
    public async Task Gateway_SetPassword_StoresItAndNeverShowsIt()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await cut.InvokeAsync(() => cut.Find("[data-testid=new-password] input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "correct horse battery" }));
        await Click(cut, () => Button(Part(cut, "gateway"), "Set password"));

        Assert.True(this.H.Running.VerifyPassword("correct horse battery"));
        Assert.Equal("Password set.", cut.TextOf("[data-testid=gateway-notice]"));
        Assert.DoesNotContain("correct horse battery", cut.Markup, StringComparison.Ordinal);
    }

    // ---- G10e Waiting for approval -----------------------------------------------------------------------------------------------------

    /// <summary>With no request waiting, the section is not drawn at all.</summary>
    [Fact]
    public void Requests_None_SectionIsNotDrawn()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Empty(cut.FindAll("[data-testid=requests]"));
        Assert.DoesNotContain("Waiting for approval", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>One request shows the heading with a count and the name, address, ingress, scopes and countdown.</summary>
    [Fact]
    public void Requests_One_ShowsNameAddressIngressScopesAndCountdown()
    {
        PairingRequest request = this.H.OpenRequest("iPad · Safari", "Direct");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        IElement row = RequestRow(cut, request.RequestId);
        Assert.Contains("Waiting for approval (1)", Part(cut, "requests").TextContent, StringComparison.Ordinal);
        Assert.Equal("iPad · Safari", TextOf(row, "name"));
        Assert.Equal("192.168.1.34", TextOf(row, "address"));
        Assert.Equal("Network (downgraded)", TextOf(row, "ingress"));
        Assert.Equal("asks Limited", TextOf(row, "scopes"));
        Assert.Equal("5:00", TextOf(row, "countdown"));
    }

    /// <summary>The countdown counts down to the expiry: 29 seconds in, 4:31 remains.</summary>
    [Fact]
    public void Requests_Countdown_ShowsTimeLeft()
    {
        PairingRequest request = this.H.OpenRequest("iPad", "Direct");
        this.H.Clock.Advance(TimeSpan.FromSeconds(29));
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("4:31", TextOf(RequestRow(cut, request.RequestId), "countdown"));
    }

    /// <summary>Ingress names read as words: this computer, Tailscale, proxy; only Direct is "downgraded" (the loopback harness bind reads as a plain network).</summary>
    [Theory]
    [InlineData("Loopback", "This computer")]
    [InlineData("Direct", "Network (downgraded)")]
    [InlineData("Managed", "Tailscale")]
    [InlineData("TrustedProxy", "Proxy")]
    public void Requests_IngressLabel_ReadsAsWords(string ingress, string label)
    {
        PairingRequest request = this.H.OpenRequest("iPad", ingress);
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal(label, TextOf(RequestRow(cut, request.RequestId), "ingress"));
    }

    /// <summary>A request from the node owner's Tailscale login is marked You, whatever the letter case.</summary>
    [Fact]
    public void Requests_OwnerLogin_IsMarkedYou()
    {
        this.H.View.TailscaleOwnerLogin = "Ada@Example.com";
        PairingRequest request = this.H.OpenRequest("iPad", "Managed", "ada@example.com");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        IElement row = RequestRow(cut, request.RequestId);
        Assert.Equal("ada@example.com", TextOf(row, "login"));
        Assert.Equal("You", TextOf(row, "login-kind"));
    }

    /// <summary>Another Tailscale login is marked Shared user.</summary>
    [Fact]
    public void Requests_OtherLogin_IsMarkedSharedUser()
    {
        this.H.View.TailscaleOwnerLogin = "ada@example.com";
        PairingRequest request = this.H.OpenRequest("iPad", "Managed", "bob@example.com");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Shared user", TextOf(RequestRow(cut, request.RequestId), "login-kind"));
    }

    /// <summary>A request with no Tailscale login draws no login.</summary>
    [Fact]
    public void Requests_NoLogin_DrawsNoLogin()
    {
        PairingRequest request = this.H.OpenRequest("iPad", "Direct");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Empty(RequestRow(cut, request.RequestId).QuerySelectorAll("[data-part=login]"));
    }

    /// <summary>An admin request says "asks Admin".</summary>
    [Fact]
    public void Requests_AdminRequest_SaysAsksAdmin()
    {
        PairingRequest request = this.H.OpenRequest("Laptop", "Direct", null, GatewayScopes.Admin);
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("asks Admin", TextOf(RequestRow(cut, request.RequestId), "scopes"));
    }

    /// <summary>The Approve menu of a Limited request offers Limited only; approving with it pairs the device Limited and clears the row.</summary>
    [Fact]
    public async Task Requests_ApproveLimited_PairsTheDevice()
    {
        PairingRequest request = this.H.OpenRequest("iPad", "Direct");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(RequestRow(cut, request.RequestId), "Approve"));
        Assert.Equal(["Limited"], cut.MenuItems().Select(item => item.TextContent.Trim()).ToList());
        await cut.ClickMenuItemAsync("Limited");

        DeviceRecord? device = this.H.Devices.Find(request.DeviceId);
        Assert.NotNull(device);
        Assert.DoesNotContain(GatewayScopes.Admin, device.Scopes);
        Assert.Empty(cut.FindAll($"[data-testid=request-row][data-request-id='{request.RequestId}']"));
    }

    /// <summary>The Approve menu of an admin request offers Limited and Admin; choosing Admin grants it.</summary>
    [Fact]
    public async Task Requests_ApproveAdmin_GrantsAdmin()
    {
        PairingRequest request = this.H.OpenRequest("Laptop", "Direct", null, GatewayScopes.Admin);
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(RequestRow(cut, request.RequestId), "Approve"));
        Assert.Equal(["Limited", "Admin"], cut.MenuItems().Select(item => item.TextContent.Trim()).ToList());
        await cut.ClickMenuItemAsync("Admin");

        Assert.Contains(GatewayScopes.Admin, this.H.Devices.Find(request.DeviceId)?.Scopes ?? []);
    }

    /// <summary>Reject ends the request, pairs nothing and clears the row.</summary>
    [Fact]
    public async Task Requests_Reject_EndsTheRequest()
    {
        PairingRequest request = this.H.OpenRequest("iPad", "Direct");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(RequestRow(cut, request.RequestId), "Reject"));

        Assert.Equal(PairingService.State.Rejected, this.H.Pairing.StateOf(request.RequestId));
        Assert.Null(this.H.Devices.Find(request.DeviceId));
        Assert.Empty(cut.FindAll("[data-testid=requests]"));
    }

    // ---- G10b Pair a device ------------------------------------------------------------------------------------------------------------

    /// <summary>The QR is a 160 pixel image of the setup link with its alt text.</summary>
    [Fact]
    public void Pair_ShowsTheQrImage()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        IElement image = cut.Find("[data-testid=qr] img");
        Assert.Equal("data:image/png;base64,AQID", image.GetAttribute("src"));
        Assert.Equal("QR code that pairs a device with Huddle", image.GetAttribute("alt"));
        Assert.Equal("160", image.GetAttribute("width"));
        Assert.Equal("160", image.GetAttribute("height"));
    }

    /// <summary>The link under the QR is the exact link the QR encodes.</summary>
    [Fact]
    public void Pair_LinkIsWhatTheQrEncodes()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        string link = cut.TextOf("[data-testid=setup-link]");
        Assert.StartsWith("https://desktop-a.tail1234.ts.net:5101" + BootstrapLinks.Suffix, link, StringComparison.Ordinal);
        Assert.Equal(link, Assert.Single(this.H.Qr.Payloads));
    }

    /// <summary>The sentence under the QR says it works once, for 10 minutes, and that approval still follows.</summary>
    [Fact]
    public void Pair_ShowsTheApprovalSentence()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("Scan to pair. Works once, for 10 minutes. You will still approve it.", cut.TextOf("[data-testid=pair-note]"));
    }

    /// <summary>New link mints another link.</summary>
    [Fact]
    public async Task Pair_NewLink_MintsAnotherLink()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();
        string first = cut.TextOf("[data-testid=setup-link]");

        await Click(cut, () => Button(Part(cut, "pair"), "New link"));

        Assert.Equal(2, this.H.Qr.Payloads.Count);
        Assert.NotEqual(first, cut.TextOf("[data-testid=setup-link]"));
    }

    /// <summary>With no address to pair against, the section says why and draws no QR.</summary>
    [Fact]
    public void Pair_NoAddress_DrawsNoQrAndSaysWhy()
    {
        this.H.View.PublicUrl = null;
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Empty(cut.FindAll("[data-testid=qr] img"));
        Assert.NotEmpty(cut.TextOf("[data-testid=pair-unavailable]"));
    }

    // ---- G10d Devices ------------------------------------------------------------------------------------------------------------------

    /// <summary>The heading counts the devices.</summary>
    [Fact]
    public void Devices_HeadingCountsDevices()
    {
        _ = this.H.PairDevice("iPhone");
        _ = this.H.PairDevice("iPad", GatewayScopes.Admin);
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Contains("Devices (2)", Part(cut, "devices").TextContent, StringComparison.Ordinal);
    }

    /// <summary>A row shows the name, Limited or Admin, how it was paired, the last address and when it was last seen.</summary>
    [Fact]
    public void Devices_Row_ShowsNameScopesViaAddressAndLastSeen()
    {
        DeviceRecord phone = this.H.PairDevice("iPhone");
        DeviceRecord laptop = this.H.PairDevice("Laptop", GatewayScopes.Admin);
        this.H.Clock.Advance(TimeSpan.FromHours(2));
        IRenderedComponent<ContainerFragment> cut = this.Render();

        IElement row = DeviceRow(cut, phone.DeviceId);
        Assert.Equal("iPhone", TextOf(row, "name"));
        Assert.Equal("Limited", TextOf(row, "scopes"));
        Assert.Equal("Paired via approval", TextOf(row, "paired-via"));
        Assert.Equal("192.168.1.34", TextOf(row, "last-address"));
        Assert.Equal("seen 2 h", TextOf(row, "last-seen"));
        Assert.Equal("Admin", TextOf(DeviceRow(cut, laptop.DeviceId), "scopes"));
    }

    /// <summary>A device seen under a minute ago says "seen now".</summary>
    [Fact]
    public void Devices_JustSeen_SaysSeenNow()
    {
        DeviceRecord phone = this.H.PairDevice("iPhone");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("seen now", TextOf(DeviceRow(cut, phone.DeviceId), "last-seen"));
    }

    /// <summary>The device this browser is paired as is marked This device; others are not.</summary>
    [Fact]
    public void Devices_ThisDevice_IsMarked()
    {
        _ = this.H.Devices.Pair(new DeviceRegistry.Registration("test-device", new byte[32], "This computer (Edge)", GatewayScopes.AdminSet, "loopback", null, "127.0.0.1", "ua/1"));
        DeviceRecord other = this.H.PairDevice("iPhone");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("This device", TextOf(DeviceRow(cut, "test-device"), "this-device"));
        Assert.Empty(DeviceRow(cut, other.DeviceId).QuerySelectorAll("[data-part=this-device]"));
    }

    /// <summary>Rename opens a name field; Save stores the new name.</summary>
    [Fact]
    public async Task Devices_Rename_StoresTheNewName()
    {
        DeviceRecord phone = this.H.PairDevice("iPhone");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(DeviceRow(cut, phone.DeviceId), "Rename"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=rename-input] input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Ada's phone" }));
        await Click(cut, () => Button(DeviceRow(cut, phone.DeviceId), "Save"));

        Assert.Equal("Ada's phone", this.H.Devices.Find(phone.DeviceId)?.Name);
        Assert.Equal("Ada's phone", TextOf(DeviceRow(cut, phone.DeviceId), "name"));
    }

    /// <summary>Rotate gives the device a new token: the old one stops working.</summary>
    [Fact]
    public async Task Devices_Rotate_InvalidatesTheOldToken()
    {
        TestDevice device = new();
        string? oldToken = this.H.Devices.Pair(new DeviceRegistry.Registration(device.DeviceId, device.PublicKey, "iPhone", GatewayScopes.Limited, "approval", null, "192.168.1.34", "ua/1"));
        Assert.NotNull(oldToken);
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(DeviceRow(cut, device.DeviceId), "Rotate"));

        Assert.Null(this.H.Devices.FindByToken(oldToken));
    }

    /// <summary>Revoke takes the device's token away and the row says Revoked.</summary>
    [Fact]
    public async Task Devices_Revoke_DropsTheToken()
    {
        DeviceRecord phone = this.H.PairDevice("iPhone");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(DeviceRow(cut, phone.DeviceId), "Revoke"));

        Assert.False(this.H.Devices.Find(phone.DeviceId)?.HasToken);
        Assert.Equal("Revoked", TextOf(DeviceRow(cut, phone.DeviceId), "revoked"));
    }

    /// <summary>Remove asks first, in the row: the prompt names the device and nothing is deleted until Remove is confirmed.</summary>
    [Fact]
    public async Task Devices_Remove_AsksInlineThenDeletes()
    {
        DeviceRecord phone = this.H.PairDevice("iPhone");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(DeviceRow(cut, phone.DeviceId), "Remove"));

        Assert.Contains("Remove iPhone? It will have to pair again.", DeviceRow(cut, phone.DeviceId).TextContent, StringComparison.Ordinal);
        Assert.NotNull(this.H.Devices.Find(phone.DeviceId));

        await Click(cut, () => Button(DeviceRow(cut, phone.DeviceId), "Remove"));

        Assert.Null(this.H.Devices.Find(phone.DeviceId));
        Assert.Empty(cut.FindAll($"[data-testid=device-row][data-device-id='{phone.DeviceId}']"));
    }

    /// <summary>Cancel in the inline confirm keeps the device.</summary>
    [Fact]
    public async Task Devices_RemoveCancelled_KeepsTheDevice()
    {
        DeviceRecord phone = this.H.PairDevice("iPhone");
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await Click(cut, () => Button(DeviceRow(cut, phone.DeviceId), "Remove"));
        await Click(cut, () => Button(DeviceRow(cut, phone.DeviceId), "Cancel"));

        Assert.NotNull(this.H.Devices.Find(phone.DeviceId));
        Assert.DoesNotContain("It will have to pair again.", DeviceRow(cut, phone.DeviceId).TextContent, StringComparison.Ordinal);
    }

    // ---- G10f Security check -----------------------------------------------------------------------------------------------------------

    /// <summary>With nothing to report, the section says so.</summary>
    [Fact]
    public void Security_Clean_SaysNoProblems()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Contains("No problems found.", Part(cut, "security").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=security-item]"));
    }

    /// <summary>An item shows its severity icon, its id, the condition and the fix.</summary>
    [Fact]
    public void Security_Item_ShowsSeverityIdConditionAndFix()
    {
        this.Start(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Team:Gateway:Bind"] = "lan" });
        IRenderedComponent<ContainerFragment> cut = this.Render();

        IElement item = cut.Find("[data-testid=security-item][data-id='gateway.downgraded_transport']");
        Assert.Equal("Warning", item.QuerySelector("[data-part=severity]")?.GetAttribute("aria-label"));
        Assert.Equal("gateway.downgraded_transport", TextOf(item, "id"));
        Assert.Equal("A LAN bind is active: traffic is not encrypted.", TextOf(item, "condition"));
        Assert.Equal("Use Tailscale Serve, which encrypts the connection.", TextOf(item, "fix"));
    }

    /// <summary>A Critical item carries the Critical icon label.</summary>
    [Fact]
    public void Security_Critical_UsesTheCriticalIcon()
    {
        this.Start(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Team:Gateway:Tailscale:Mode"] = "funnel" });
        this.H.View.TailscaleState = TailscaleState.Running;
        IRenderedComponent<ContainerFragment> cut = this.Render();

        IElement item = cut.Find("[data-testid=security-item][data-id='gateway.tailscale_funnel']");
        Assert.Equal("Critical", item.QuerySelector("[data-part=severity]")?.GetAttribute("aria-label"));
    }
}
