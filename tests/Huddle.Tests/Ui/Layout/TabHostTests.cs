using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>Pins the tab strip as the Human meets it: <see cref="TabHost"/>, <see cref="TabStrip"/> and <see cref="NavTabLink"/> over a real <see cref="TabService"/>.</summary>
public sealed class TabHostTests
{
    private static MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<TabService>();
        ctx.Services.AddSingleton(StubPages.Routes());
        return ctx;
    }

    private static IRenderedComponent<TabHost> RenderHost(MudBunitContext ctx) => ctx.Render<TabHost>();

    // Renders the host with the popover provider a Tabs context menu paints into.
    private static IRenderedComponent<ContainerFragment> RenderHostWithMenus(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TabHost>(0);
            builder.CloseComponent();
        });

    private static IReadOnlyList<IElement> MenuItems(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("div.mud-menu-item");

    private static string Label(IElement item) => item.TextContent.Trim();

    private static string ActiveId(TabService tabs) => tabs.Active?.Id ?? throw new InvalidOperationException("No Tab is active.");

    /// <summary>The location the circuit starts on opens as an active, italic preview Tab.</summary>
    [Fact]
    public async Task StartingLocation_OpensAnActivePreviewTab()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teammates");

        var cut = RenderHost(ctx);

        var tab = Assert.Single(cut.FindAll(".tab-item"));
        Assert.Contains("active", tab.ClassList);
        Assert.Contains("preview", tab.ClassList);
        Assert.Equal("Teammates", tab.QuerySelector(".tab-title")?.TextContent);
    }

    /// <summary>Navigating to another page replaces the preview Tab instead of adding one.</summary>
    [Fact]
    public async Task Navigating_ReplacesThePreviewTab()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);

        nav.NavigateTo("/settings");

        cut.WaitForAssertion(() => Assert.Equal("Settings", Assert.Single(cut.FindAll(".tab-item")).QuerySelector(".tab-title")?.TextContent));
    }

    /// <summary>A double-click on a Tab makes it permanent, so the next page opens beside it.</summary>
    [Fact]
    public async Task DoubleClickingATab_KeepsIt()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);

        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");

        cut.WaitForAssertion(() =>
        {
            var tabs = cut.FindAll(".tab-item");
            Assert.Equal(2, tabs.Count);
            Assert.DoesNotContain("preview", tabs[0].ClassList);
            Assert.Contains("preview", tabs[1].ClassList);
        });
    }

    /// <summary>Closing the only Tab shows the empty state and hides the page.</summary>
    [Fact]
    public async Task ClosingTheLastTab_ShowsTheEmptyState()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teammates");
        var cut = RenderHost(ctx);

        cut.Find(".tab-close").Click();

        Assert.Empty(cut.FindAll(".tab-item"));
        Assert.NotNull(cut.Find(".tab-empty"));
        Assert.Empty(cut.FindAll(".stub-page"));
    }

    /// <summary>Closing the active Tab navigates the router to the Tab that becomes active.</summary>
    [Fact]
    public async Task ClosingTheActiveTab_NavigatesToTheNeighbour()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));

        cut.FindAll(".tab-close")[1].Click();

        Assert.Equal("/teammates", new Uri(nav.Uri).AbsolutePath);
        Assert.Equal("teammates", tabs.Active?.Key);
    }

    /// <summary>A menu link opens its page with the title the menu shows, not the address's fallback.</summary>
    [Fact]
    public async Task NavTabLink_Click_OpensTheTabWithTheMenusTitle()
    {
        await using MudBunitContext ctx = NewContext();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        var cut = ctx.Render<NavTabLink>(parameters => parameters
            .Add(link => link.Href, "/rooms/ab12")
            .Add(link => link.TabTitle, "Design")
            .Add(link => link.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Design"))));

        cut.Find(".nav-tab-link").Click();

        Tab tab = Assert.Single(tabs.Tabs);
        Assert.Equal("Design", tab.Title);
        Assert.True(tab.IsPreview);
    }

    /// <summary>A double-click on a menu link makes its Tab permanent.</summary>
    [Fact]
    public async Task NavTabLink_DoubleClick_KeepsTheTab()
    {
        await using MudBunitContext ctx = NewContext();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        var cut = ctx.Render<NavTabLink>(parameters => parameters
            .Add(link => link.Href, "/settings")
            .Add(link => link.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Settings"))));

        cut.Find(".nav-tab-link").Click();
        cut.Find(".nav-tab-link").DoubleClick();

        Assert.False(Assert.Single(tabs.Tabs).IsPreview);
    }

    /// <summary>The shortcut script's Alt+W closes the active Tab and the router follows the neighbour.</summary>
    [Fact]
    public async Task Shortcut_Close_ClosesTheActiveTabAndNavigates()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));

        await cut.InvokeAsync(() => cut.Instance.ShortcutAsync("close"));

        Assert.Equal("/teammates", new Uri(nav.Uri).AbsolutePath);
        Assert.Equal("teammates", tabs.Active?.Key);
    }

    /// <summary>The shortcut script's next and previous move the active Tab and the router follows.</summary>
    [Fact]
    public async Task Shortcut_NextAndPrevious_MoveBetweenTabs()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));

        await cut.InvokeAsync(() => cut.Instance.ShortcutAsync("next"));
        Assert.Equal("/teammates", new Uri(nav.Uri).AbsolutePath);

        await cut.InvokeAsync(() => cut.Instance.ShortcutAsync("previous"));
        Assert.Equal("/settings", new Uri(nav.Uri).AbsolutePath);
    }

    /// <summary>The script is attached once the host has rendered.</summary>
    [Fact]
    public async Task FirstRender_AttachesTheShortcutScript()
    {
        await using MudBunitContext ctx = NewContext();

        _ = RenderHost(ctx);

        ctx.JSInterop.VerifyInvoke("huddleTabs.attach");
    }

    /// <summary>The page of the active Tab is mounted, resolved from the Tab's address with its route values.</summary>
    [Fact]
    public async Task ActiveTab_MountsItsPageWithTheRouteValues()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/ab12");

        var cut = RenderHost(ctx);

        Assert.Equal("ab12", Assert.Single(cut.FindAll(".stub-room")).TextContent);
    }

    /// <summary>An address no page answers is said so in the pane, not rendered as a blank.</summary>
    [Fact]
    public async Task UnknownAddress_ShowsThatThereIsNoPage()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/nowhere/at/all");

        var cut = RenderHost(ctx);

        Assert.Equal("There is no page at /nowhere/at/all.", cut.Find(".tab-empty").TextContent.Trim());
    }

    /// <summary>A Tab moved right opens a second pane, and both panes show their own page at once.</summary>
    [Fact]
    public async Task MovingATabRight_ShowsTwoPanesWithTwoPages()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));

        await cut.InvokeAsync(() => tabs.MoveRight(ActiveId(tabs)));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll(".tab-group").Count);
            Assert.Single(cut.FindAll("#page-teammates"));
            Assert.Single(cut.FindAll("#page-settings"));
        });
    }

    /// <summary>Pressing in the unfocused pane focuses it and the router follows to that pane's active Tab.</summary>
    [Fact]
    public async Task PressingInTheOtherPane_FocusesItAndNavigates()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));
        await cut.InvokeAsync(() => tabs.MoveRight(ActiveId(tabs)));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-group").Count));

        await cut.FindAll(".tab-group")[0].TriggerEventAsync("onpointerdown", new PointerEventArgs());

        Assert.Equal("/teammates", new Uri(nav.Uri).AbsolutePath);
        Assert.Equal("teammates", tabs.Active?.Key);
        cut.WaitForAssertion(() => Assert.Contains("focused", cut.FindAll(".tab-group")[0].ClassList));
    }

    /// <summary>The focused pane is not listening for presses: only the other pane has a handler.</summary>
    [Fact]
    public async Task FocusedPane_HasNoPressHandler()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));
        await cut.InvokeAsync(() => tabs.MoveRight(ActiveId(tabs)));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-group").Count));

        IReadOnlyList<IElement> panes = cut.FindAll(".tab-group");

        Assert.Contains("focused", panes[1].ClassList);
        Assert.Null(panes[1].GetAttribute("blazor:onpointerdown"));
        Assert.NotNull(panes[0].GetAttribute("blazor:onpointerdown"));
    }

    /// <summary>A right-click on a Tab offers Pin, Move Left, Move Right and Close.</summary>
    [Fact]
    public async Task RightClickingATab_OffersTheTabMenu()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teammates");
        var cut = RenderHostWithMenus(ctx);

        await cut.InvokeAsync(() => cut.Find(".tab-item").ContextMenuAsync());

        Assert.Equal(["Pin Tab", "Move Left", "Move Right", "Close"], MenuItems(cut).Select(Label));
    }

    /// <summary>With one Tab alone in the only pane there is nowhere to move it, so both moves are disabled.</summary>
    [Fact]
    public async Task TheTabMenu_DisablesMovesForALoneTab()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teammates");
        var cut = RenderHostWithMenus(ctx);

        await cut.InvokeAsync(() => cut.Find(".tab-item").ContextMenuAsync());

        Assert.Equal(["Move Left", "Move Right"], MenuItems(cut).Where(static item => item.ClassList.Contains("mud-disabled")).Select(Label));
    }

    /// <summary>Pin Tab pins the Tab: its x goes, and the menu then offers Unpin Tab.</summary>
    [Fact]
    public async Task PinTab_PinsTheTab()
    {
        await using MudBunitContext ctx = NewContext();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teammates");
        var cut = RenderHostWithMenus(ctx);
        await cut.InvokeAsync(() => cut.Find(".tab-item").ContextMenuAsync());

        await cut.InvokeAsync(() => MenuItems(cut).First(item => Label(item) == "Pin Tab").ClickAsync());

        Assert.True(Assert.Single(tabs.Tabs).IsPinned);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("pinned", cut.Find(".tab-item").ClassList);
            Assert.Empty(cut.FindAll(".tab-close"));
        });
    }

    /// <summary>Move Right on a Tab with company opens a second pane holding it.</summary>
    [Fact]
    public async Task MoveRight_FromTheMenu_OpensASecondPane()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        nav.NavigateTo("/teammates");
        var cut = RenderHostWithMenus(ctx);
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));
        await cut.InvokeAsync(() => cut.FindAll(".tab-item")[1].ContextMenuAsync());

        await cut.InvokeAsync(() => MenuItems(cut).First(item => Label(item) == "Move Right").ClickAsync());

        Assert.Equal(2, tabs.Groups.Count);
        Assert.Equal("settings", tabs.Groups[1].Active?.Key);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-group").Count));
    }

    /// <summary>Close in the menu closes that Tab.</summary>
    [Fact]
    public async Task Close_FromTheMenu_ClosesTheTab()
    {
        await using MudBunitContext ctx = NewContext();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teammates");
        var cut = RenderHostWithMenus(ctx);
        await cut.InvokeAsync(() => cut.Find(".tab-item").ContextMenuAsync());

        await cut.InvokeAsync(() => MenuItems(cut).First(item => Label(item) == "Close").ClickAsync());

        Assert.Empty(tabs.Tabs);
    }

    /// <summary>The hover toolbar offers the chevron always, and each move button only when that move is possible.</summary>
    [Fact]
    public async Task HoverToolbar_DrawsOnlyThePossibleMoves()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);

        Assert.Equal(["Menu for Teammates"], cut.FindAll(".tab-action").Select(static button => button.GetAttribute("aria-label")));

        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));
        await cut.InvokeAsync(() => tabs.MoveRight(ActiveId(tabs)));

        cut.WaitForAssertion(() => Assert.Equal(
            ["Menu for Teammates", "Move Teammates right", "Menu for Settings", "Move Settings left"],
            cut.FindAll(".tab-action").Select(static button => button.GetAttribute("aria-label"))));
    }

    /// <summary>A move button on the toolbar moves its Tab without activating anything else.</summary>
    [Fact]
    public async Task HoverToolbar_MoveRight_MovesTheTab()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));

        cut.Find("button[aria-label='Move Settings right']").Click();

        Assert.Equal(2, tabs.Groups.Count);
        Assert.Equal("settings", tabs.Groups[1].Active?.Key);
    }

    /// <summary>The chevron opens the same menu as a right-click, and a pinned Tab has no toolbar.</summary>
    [Fact]
    public async Task HoverToolbar_Chevron_OpensTheMenu_AndPinnedTabsHaveNone()
    {
        await using MudBunitContext ctx = NewContext();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teammates");
        var cut = RenderHostWithMenus(ctx);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Menu for Teammates']").ClickAsync());

        Assert.Equal(["Pin Tab", "Move Left", "Move Right", "Close"], MenuItems(cut).Select(Label));
        await cut.InvokeAsync(() => MenuItems(cut).First(item => Label(item) == "Pin Tab").ClickAsync());
        Assert.True(Assert.Single(tabs.Tabs).IsPinned);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".tab-actions")));
    }

    /// <summary>A page learns its own address from the cascaded context, not from the browser URL.</summary>
    [Fact]
    public async Task Page_ReceivesItsTabsAddressAsContext()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/tasks/new");

        var cut = RenderHost(ctx);

        Assert.Equal("/tasks/new", Assert.Single(cut.FindAll(".stub-tasks-new")).TextContent);
    }
}
