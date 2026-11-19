using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Issue #451: after a navigation the router focuses a heading, and with two panes it must be the focused pane's.
/// <c>FocusOnNavigate</c> with a bare <c>h1</c> focused the first pane's heading, whose <c>focusin</c> refocused that pane and
/// navigated back, so a press in the second pane never kept the focus or the Right sidebar.
/// </summary>
public sealed class PaneFocusTests
{
    /// <summary>The router focuses the heading inside the focused pane, not the first heading in the document.</summary>
    [Fact]
    public void Routes_FocusOnNavigate_TargetsTheFocusedPanesHeading()
    {
        string routes = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Routes.razor"));

        Assert.Matches(@"<FocusOnNavigate\s+RouteData=""routeData""\s+Selector=""\.tab-group\.focused h1""\s*/>", routes);
    }

    /// <summary>A single pane is the focused pane, so the scoped selector still finds its page's heading after a navigation.</summary>
    [Fact]
    public async Task SinglePane_IsTheFocusedPane()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTabs().WithRoutes();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/scheduled");

        var cut = ctx.Render<TabHost>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".tab-group.focused")));
    }
}
