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
/// Manual-run fixes to <see cref="RemoteAccessPanel"/>: the downgraded notice on the Gateway card (bug 2), the human ingress words per bind
/// (bug 4) and the setup link that replaces itself once it is spent (bug 5).
/// </summary>
public sealed class RemoteAccessPanelFixTests : IAsyncDisposable
{
    private readonly MudBunitContext ctx = new();
    private GatewayAdminHarness? harness;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.ctx.DisposeAsync();
        this.harness?.Dispose();
    }

    private GatewayAdminHarness H => this.harness ?? throw new InvalidOperationException("Render first.");

    private IRenderedComponent<ContainerFragment> Render(string bind = "loopback", string? extraKey = null, string? extraValue = null, string tailscaleMode = "off")
    {
        Dictionary<string, string?> config = new(StringComparer.Ordinal)
        {
            ["Team:Gateway:Bind"] = bind,
            ["Team:Gateway:Tailscale:Mode"] = tailscaleMode,
        };
        if (extraKey is not null)
        {
            config[extraKey] = extraValue;
        }

        this.harness = new GatewayAdminHarness(config);
        this.ctx.Services.AddSingleton(this.harness.Service);
        this.ctx.Services.AddSingleton<IGatewayRuntimeView>(this.harness.View);
        this.ctx.Services.AddSingleton<TimeProvider>(this.H.Clock);
        return this.ctx.RenderWithPopovers<RemoteAccessPanel>();
    }

    private static IElement Part(IRenderedComponent<ContainerFragment> cut, string testId) => cut.Find($"[data-testid={testId}]");

    private static string IngressOf(IRenderedComponent<ContainerFragment> cut, string requestId) =>
        cut.Find($"[data-testid=request-row][data-request-id='{requestId}'] [data-part=ingress]").TextContent.Trim();

    // ---- Bug 2: the Gateway card says downgraded -----------------------------------------------------------------------------------------

    /// <summary>A running lan bind shows the plain HTTP warning on the Gateway card, and the state label says downgraded.</summary>
    [Fact]
    public void Gateway_LanBind_ShowsDowngradedNoticeAndLabel()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render("lan");

        Assert.Equal("Plain HTTP: anyone on this network can read what Huddle sends.", Part(cut, "downgraded-notice").TextContent.Trim());
        Assert.Equal("Running (downgraded)", Part(cut, "status").TextContent.Trim());
    }

    /// <summary>A running tailnet bind shows the same notice.</summary>
    [Fact]
    public void Gateway_TailnetBind_ShowsDowngradedNotice()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render("tailnet");

        Assert.Equal("Plain HTTP: anyone on this network can read what Huddle sends.", Part(cut, "downgraded-notice").TextContent.Trim());
    }

    /// <summary>A running custom bind shows the same notice.</summary>
    [Fact]
    public void Gateway_CustomBind_ShowsDowngradedNotice()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render("custom", "Team:Gateway:CustomBindHost", "10.0.0.5");

        Assert.Equal("Plain HTTP: anyone on this network can read what Huddle sends.", Part(cut, "downgraded-notice").TextContent.Trim());
    }

    /// <summary>A loopback bind has no notice and the state label does not say downgraded.</summary>
    [Fact]
    public void Gateway_LoopbackBind_HasNoDowngradedNotice()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render("loopback");

        Assert.Empty(cut.FindAll("[data-testid=downgraded-notice]"));
        Assert.DoesNotContain("downgraded", Part(cut, "status").TextContent, StringComparison.Ordinal);
    }

    // ---- Bug 4: ingress words per bind ---------------------------------------------------------------------------------------------------

    /// <summary>A direct request on a lan bind reads "LAN (downgraded)".</summary>
    [Fact]
    public void Requests_DirectOnLan_ReadsLan()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render("lan");
        PairingRequest request = this.H.OpenRequest("iPad", "Direct");
        this.H.Clock.Advance(TimeSpan.FromSeconds(1));

        cut.WaitForAssertion(() => Assert.Equal("LAN (downgraded)", IngressOf(cut, request.RequestId)));
    }

    /// <summary>A direct request on a tailnet bind reads "Tailscale address (downgraded)".</summary>
    [Fact]
    public void Requests_DirectOnTailnet_ReadsTailscaleAddress()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render("tailnet");
        PairingRequest request = this.H.OpenRequest("iPad", "Direct");
        this.H.Clock.Advance(TimeSpan.FromSeconds(1));

        cut.WaitForAssertion(() => Assert.Equal("Tailscale address (downgraded)", IngressOf(cut, request.RequestId)));
    }

    /// <summary>A direct request on a custom bind reads "Network (downgraded)".</summary>
    [Fact]
    public void Requests_DirectOnCustom_ReadsNetwork()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render("custom", "Team:Gateway:CustomBindHost", "10.0.0.5");
        PairingRequest request = this.H.OpenRequest("iPad", "Direct");
        this.H.Clock.Advance(TimeSpan.FromSeconds(1));

        cut.WaitForAssertion(() => Assert.Equal("Network (downgraded)", IngressOf(cut, request.RequestId)));
    }

    // ---- Bug 5: a spent setup link is replaced -------------------------------------------------------------------------------------------

    private static string LinkOf(IRenderedComponent<ContainerFragment> cut) => Part(cut, "setup-link").TextContent.Trim();

    /// <summary>Once the shown setup link is redeemed the card shows a new link (and a new QR) on the next refresh.</summary>
    [Fact]
    public void Pair_LinkRedeemed_IsReplacedWithAFreshLink()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render("lan", "Team:Gateway:PublicOrigin", "http://192.168.1.20:5101");
        string first = LinkOf(cut);
        int tokenAt = first.IndexOf("bootstrapToken=", StringComparison.Ordinal) + "bootstrapToken=".Length;
        string token = first[tokenAt..];

        Assert.True(this.H.Tokens.Redeem(token, "remote-device").Valid);
        this.H.Clock.Advance(TimeSpan.FromSeconds(1));

        cut.WaitForAssertion(() => Assert.NotEqual(first, LinkOf(cut)));
        Assert.Equal(first[..tokenAt], LinkOf(cut)[..tokenAt]);
    }

    /// <summary>A link that nobody redeemed stays on the card.</summary>
    [Fact]
    public void Pair_LinkUntouched_IsKept()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render("lan", "Team:Gateway:PublicOrigin", "http://192.168.1.20:5101");
        string first = LinkOf(cut);

        this.H.Clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(first, LinkOf(cut));
    }
}
