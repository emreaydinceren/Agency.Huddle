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
/// Strict local on the Remote access panel (ra-strict-local.md): the switch at the top of the Gateway card with its exact strings, the
/// controls it disables, the cards it hides, the restart line for open listeners and the Save flow.
/// </summary>
public sealed class StrictLocalPanelTests : IAsyncDisposable
{
    private const string HintText = "Turn off Strict local to set up remote access.";
    private const string HelperText = "Only this computer can use Huddle, with no sign-in. Turn off to let other devices connect.";

    private readonly MudBunitContext ctx = new();
    private GatewayAdminHarness? harness;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.ctx.DisposeAsync();
        this.harness?.Dispose();
    }

    private GatewayAdminHarness Start(bool strict, Dictionary<string, string?>? extra = null)
    {
        Dictionary<string, string?> config = new(extra ?? [], StringComparer.Ordinal) { ["Team:Gateway:StrictLocal"] = strict ? "true" : "false" };
        this.harness = new GatewayAdminHarness(config);
        this.ctx.Services.AddSingleton(this.harness.Service);
        this.ctx.Services.AddSingleton<IGatewayRuntimeView>(this.harness.View);
        this.ctx.Services.AddSingleton<TimeProvider>(this.harness.Clock);
        return this.harness;
    }

    private IRenderedComponent<ContainerFragment> Render() => this.ctx.RenderWithPopovers<RemoteAccessPanel>();

    /// <summary>The switch is the first control of the Gateway card and carries the label and the helper text.</summary>
    [Fact]
    public void Switch_HasTheLabelAndTheHelperText()
    {
        _ = this.Start(strict: true);

        IRenderedComponent<ContainerFragment> cut = this.Render();

        IElement strict = cut.Find("[data-testid=strict-local]");
        Assert.Equal("Strict local", strict.TextContent.Trim());
        Assert.Equal(HelperText, cut.TextOf("[data-testid=strict-local-helper]"));
        Assert.True(cut.Find("[data-testid=gateway]").QuerySelector("[data-testid=strict-local]") is not null);
    }

    /// <summary>While strict local is on, one line explains it, the connection controls are disabled and Pair a device is not drawn.</summary>
    [Fact]
    public void StrictLocalOn_DisablesTheControlsAndHidesPairing()
    {
        _ = this.Start(strict: true);

        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal(HintText, cut.TextOf("[data-testid=strict-local-hint]"));
        Assert.NotEmpty(cut.FindAll("[data-testid=stored-bind] .mud-disabled"));
        Assert.NotEmpty(cut.FindAll("[data-testid=allow-tailscale] .mud-disabled"));
        Assert.Empty(cut.FindAll("[data-testid=pair]"));
        Assert.Empty(cut.FindAll("[data-testid=requests]"));
        Assert.NotEmpty(cut.FindAll("[data-testid=devices]"));
    }

    /// <summary>With strict local off, there is no hint, the controls work and Pair a device is drawn.</summary>
    [Fact]
    public void StrictLocalOff_ShowsTheRemoteControls()
    {
        _ = this.Start(strict: false);

        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Empty(cut.FindAll("[data-testid=strict-local-hint]"));
        Assert.Empty(cut.FindAll("[data-testid=stored-bind] .mud-disabled"));
        Assert.NotEmpty(cut.FindAll("[data-testid=pair]"));
    }

    /// <summary>Saving with the switch turned on applies now, says so and leaves a network listener's restart line when one is still bound.</summary>
    [Fact]
    public async Task SwitchOn_Save_AppliesNowAndAsksForARestartWhileListenersRun()
    {
        GatewayAdminHarness h = this.Start(strict: false, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Team:Gateway:Bind"] = "lan" });
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await cut.InvokeAsync(() => cut.Find("[data-testid=strict-local] input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true }));
        await cut.InvokeAsync(() => cut.Find("[data-testid=gateway]").QuerySelectorAll("button").Single(static button => button.TextContent.Trim() == "Save").ClickAsync());

        Assert.True(h.Running.Options.StrictLocal);
        Assert.Equal("Saved. Applies now: Strict local", cut.TextOf("[data-testid=save-result]"));
        Assert.Equal("Restart Huddle to close the network listeners.", cut.TextOf("[data-testid=listeners-line]"));
    }

    /// <summary>Strict local from the start has no network listener, so there is no restart line.</summary>
    [Fact]
    public void StrictLocalAtStart_HasNoListenersLine()
    {
        _ = this.Start(strict: true, new Dictionary<string, string?>(StringComparer.Ordinal) { ["Team:Gateway:Bind"] = "lan" });

        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Empty(cut.FindAll("[data-testid=listeners-line]"));
    }

    /// <summary>Saving with the switch turned off is listed for the next start, with the usual restart line.</summary>
    [Fact]
    public async Task SwitchOff_Save_AsksForARestart()
    {
        GatewayAdminHarness h = this.Start(strict: true);
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await cut.InvokeAsync(() => cut.Find("[data-testid=strict-local] input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false }));
        await cut.InvokeAsync(() => cut.Find("[data-testid=gateway]").QuerySelectorAll("button").Single(static button => button.TextContent.Trim() == "Save").ClickAsync());

        Assert.True(h.Running.Options.StrictLocal);
        Assert.Equal("Saved. Applies at the next start: Strict local", cut.TextOf("[data-testid=save-result]"));
        Assert.Equal("Restart Huddle to apply: Strict local on → off", cut.TextOf("[data-testid=restart-line]"));
    }

    /// <summary>The Security check lists strict local as its Info item.</summary>
    [Fact]
    public void SecurityCheck_ListsTheStrictLocalItem()
    {
        _ = this.Start(strict: true);

        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.NotEmpty(cut.FindAll("[data-testid=security-item][data-id='gateway.strict_local']"));
    }
}
