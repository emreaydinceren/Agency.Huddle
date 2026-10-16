using Bunit;
using MudBlazor;
using Agency.Huddle.App.Components.Sidebar;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="SidebarPanelFrame"/>: the host-owned chrome of a panel (Spec D-3, D-8): title, count and collapse.</summary>
public sealed class SidebarPanelFrameTests
{
    private static IRenderedComponent<SidebarPanelFrame> RenderFrame(MudBunitContext ctx, int? countToSet = null) =>
        ctx.Render<SidebarPanelFrame>(p => p
            .Add(c => c.PanelId, "probe")
            .Add(c => c.Title, "Probe")
            .Add(c => c.Icon, Icons.Material.Filled.Toc)
            .AddChildContent<ProbePanel>(s => s.Add(c => c.CountToSet, countToSet)));

    /// <summary>A count the panel reports through its context is drawn in the header.</summary>
    [Fact]
    public async Task Frame_ShowsCountAfterSetCount()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<SidebarPanelFrame> frame = RenderFrame(ctx, 3);

        await frame.Find(".probe-set").ClickAsync(new());

        frame.WaitForAssertion(() => Assert.Equal("3", frame.Find(".sidebar-panel-count").TextContent.Trim()));
    }

    /// <summary>A panel that reports no count, or takes its count back, shows none.</summary>
    [Fact]
    public async Task Frame_NullCount_ShowsNoCount()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<SidebarPanelFrame> frame = RenderFrame(ctx, 3);
        Assert.Empty(frame.FindAll(".sidebar-panel-count"));
        await frame.Find(".probe-set").ClickAsync(new());
        frame.WaitForAssertion(() => Assert.Single(frame.FindAll(".sidebar-panel-count")));

        await frame.Find(".probe-clear").ClickAsync(new());

        frame.WaitForAssertion(() => Assert.Empty(frame.FindAll(".sidebar-panel-count")));
    }

    /// <summary>Collapsing hides the body but keeps the panel mounted, so it keeps its state and subscriptions.</summary>
    [Fact]
    public async Task Frame_Collapse_HidesBodyButKeepsItMounted()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<SidebarPanelFrame> frame = RenderFrame(ctx);
        Assert.False(frame.Find(".sidebar-panel-body").HasAttribute("hidden"));

        await frame.Find("button.sidebar-panel-header").ClickAsync(new());

        frame.WaitForAssertion(() => Assert.True(frame.Find(".sidebar-panel-body").HasAttribute("hidden")));
        Assert.Single(frame.FindAll(".probe"));
        Assert.Equal("1", frame.Find(".probe").GetAttribute("data-inits"));
    }

    /// <summary>The header button says whether the body is shown, and the click flips it.</summary>
    [Fact]
    public async Task Frame_HeaderButton_HasExpandedState()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<SidebarPanelFrame> frame = RenderFrame(ctx);
        Assert.Equal("true", frame.Find("button.sidebar-panel-header").GetAttribute("aria-expanded"));

        await frame.Find("button.sidebar-panel-header").ClickAsync(new());

        frame.WaitForAssertion(() => Assert.Equal("false", frame.Find("button.sidebar-panel-header").GetAttribute("aria-expanded")));
        await frame.Find("button.sidebar-panel-header").ClickAsync(new());
        frame.WaitForAssertion(() => Assert.Equal("true", frame.Find("button.sidebar-panel-header").GetAttribute("aria-expanded")));
    }

    /// <summary>A panel the viewer collapsed earlier starts collapsed, read from the remembered state by panel id.</summary>
    [Fact]
    public async Task Frame_RememberedCollapse_StartsCollapsed()
    {
        await using MudBunitContext ctx = new();
        ctx.JSInterop.Setup<bool>("huddleSidebar.isCollapsed", _ => true).SetResult(true);

        IRenderedComponent<SidebarPanelFrame> frame = RenderFrame(ctx);

        frame.WaitForAssertion(() => Assert.Equal("false", frame.Find("button.sidebar-panel-header").GetAttribute("aria-expanded")));
        Assert.True(frame.Find(".sidebar-panel-body").HasAttribute("hidden"));
    }
}
