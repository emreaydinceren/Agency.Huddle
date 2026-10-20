using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Pins the guard on closing a Tab whose page has unsaved edits (<see cref="TabService.SetDirty"/>, read view spec OP-1): the close
/// button and Alt+W ask "Discard changes?" first, Cancel leaves the Tab open, Discard closes it, and a Tab with nothing pending closes at once.
/// </summary>
public sealed class TabCloseGuardTests
{
    private const string Actions = ".mud-dialog-actions button";

    /// <summary>Renders the host with the dialog provider the confirmation paints into, on two permanent Tabs, the second active.</summary>
    private static IRenderedComponent<ContainerFragment> RenderTwoTabs(MudBunitContext ctx, out TabService tabs)
    {
        tabs = ctx.Services.GetRequiredService<TabService>();
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teammates");
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TabHost>(0);
            builder.CloseComponent();
        });
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));
        return cut;
    }

    /// <summary>The close button on a Tab with unsaved edits asks first, and Cancel leaves the Tab open.</summary>
    [Fact]
    public async Task CloseButton_DirtyTab_AsksAndCancelKeepsIt()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTabs().WithRoutes();
        IRenderedComponent<ContainerFragment> cut = RenderTwoTabs(ctx, out TabService tabs);
        tabs.SetDirty("settings", true);

        _ = cut.InvokeAsync(() => cut.FindAll(".tab-close")[1].ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("Discard changes?", cut.Find(".mud-dialog-title").TextContent.Trim());
        await cut.InvokeAsync(() => cut.FindAll(Actions)[0].ClickAsync());
        Assert.Equal(2, cut.FindAll(".tab-item").Count);
        Assert.True(tabs.IsDirty("settings"));
    }

    /// <summary>Choosing Discard closes the Tab with unsaved edits.</summary>
    [Fact]
    public async Task CloseButton_DirtyTab_DiscardClosesIt()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTabs().WithRoutes();
        IRenderedComponent<ContainerFragment> cut = RenderTwoTabs(ctx, out TabService tabs);
        tabs.SetDirty("settings", true);

        _ = cut.InvokeAsync(() => cut.FindAll(".tab-close")[1].ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.FindAll(Actions)[1].ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".tab-item")));
    }

    /// <summary>A Tab with nothing pending closes without a question.</summary>
    [Fact]
    public async Task CloseButton_CleanTab_ClosesAtOnce()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTabs().WithRoutes();
        IRenderedComponent<ContainerFragment> cut = RenderTwoTabs(ctx, out _);

        await cut.InvokeAsync(() => cut.FindAll(".tab-close")[1].ClickAsync());

        Assert.Single(cut.FindAll(".tab-item"));
        Assert.Empty(cut.FindAll(".mud-dialog-title"));
    }

    /// <summary>Alt+W on a Tab with unsaved edits asks first and leaves the Tab open on Cancel.</summary>
    [Fact]
    public async Task ShortcutClose_DirtyTab_AsksAndCancelKeepsIt()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTabs().WithRoutes();
        IRenderedComponent<ContainerFragment> cut = RenderTwoTabs(ctx, out TabService tabs);
        tabs.SetDirty("settings", true);
        TabHost host = cut.FindComponent<TabHost>().Instance;

        _ = cut.InvokeAsync(() => host.ShortcutAsync("close"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.FindAll(Actions)[0].ClickAsync());

        Assert.Equal(2, cut.FindAll(".tab-item").Count);
    }

    /// <summary>The dirty mark is per Tab key, can be cleared, and clearing an unmarked key is harmless.</summary>
    [Fact]
    public void SetDirty_IsPerKeyAndClearable()
    {
        TabService tabs = new();

        tabs.SetDirty("task-edit:PLAT-0001", true);

        Assert.True(tabs.IsDirty("task-edit:PLAT-0001"));
        Assert.False(tabs.IsDirty("task-edit:PLAT-0002"));
        tabs.SetDirty("task-edit:PLAT-0001", false);
        tabs.SetDirty("never-marked", false);
        Assert.False(tabs.IsDirty("task-edit:PLAT-0001"));
    }
}
