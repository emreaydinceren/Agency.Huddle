using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Layout;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Pins the Activity bar (<c>S5</c>): one pressed button per selected Sidebar view, a click on the selected one
/// collapses the Sidebar (<c>S2</c>), and Archived and Settings sit in its bottom toolbar.
/// </summary>
public sealed class ActivityBarTests
{
    /// <summary>A click on the selected view collapses the Sidebar; a click on any other view, or while collapsed, selects it.</summary>
    /// <param name="current">The view selected before the click, or <see langword="null"/> while collapsed.</param>
    /// <param name="clicked">The view clicked.</param>
    /// <param name="expected">The view selected after the click, or <see langword="null"/> when collapsed.</param>
    [Theory]
    [InlineData(SidebarView.Chats, SidebarView.Chats, null)]
    [InlineData(SidebarView.Chats, SidebarView.Teams, SidebarView.Teams)]
    [InlineData(null, SidebarView.Library, SidebarView.Library)]
    public void Next_TogglesOnlyTheSelectedView(SidebarView? current, SidebarView clicked, SidebarView? expected)
    {
        Assert.Equal(expected, SidebarViews.Next(current, clicked));
    }

    /// <summary>With Tasks, Library and Jobs turned off, their buttons are left out and the other three keep their order.</summary>
    [Fact]
    public void Available_FeaturesOff_LeavesOutTasksLibraryAndScheduled()
    {
        TeamOptions options = new();
        options.Tasks.Enabled = false;
        options.Library.Enabled = false;
        options.Jobs.Enabled = false;

        IEnumerable<SidebarView> views = SidebarViews.Available(options).Select(static item => item.View);

        Assert.Equal([SidebarView.Chats, SidebarView.Teams, SidebarView.Agents], views);
    }

    /// <summary>With every feature on, Scheduled is the last view button, after Library.</summary>
    [Fact]
    public void Available_JobsOn_PutsScheduledLast()
    {
        IEnumerable<SidebarView> views = SidebarViews.Available(new TeamOptions()).Select(static item => item.View);

        Assert.Equal([SidebarView.Chats, SidebarView.Teams, SidebarView.Agents, SidebarView.Tasks, SidebarView.Library, SidebarView.Scheduled], views);
    }

    /// <summary>Only the selected view's button is pressed, and every button names the Sidebar it controls.</summary>
    [Fact]
    public async Task Render_SelectedView_IsTheOnlyPressedButton()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTeamOptions(options => { options.Tasks.Enabled = true; options.Library.Enabled = true; });

        IRenderedComponent<ActivityBar> cut = ctx.Render<ActivityBar>(p => p.Add(c => c.Selected, SidebarView.Teams));

        IReadOnlyList<IElement> buttons = cut.FindAll("button.activity-bar-item");
        Assert.Equal(6, buttons.Count);
        IElement pressed = Assert.Single(buttons, static b => b.GetAttribute("aria-pressed") == "true");
        Assert.Equal("Teams", pressed.GetAttribute("aria-label"));
        Assert.Contains("activity-bar-item-selected", pressed.ClassList);
        Assert.All(buttons, static b => Assert.Equal(ActivityBar.SidebarElementId, b.GetAttribute("aria-controls")));
    }

    /// <summary>Clicking the selected button reports no view (collapse); clicking another reports that view.</summary>
    [Fact]
    public async Task Click_ReportsTheNextView()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTeamOptions(options => { options.Tasks.Enabled = true; options.Library.Enabled = true; });
        List<SidebarView?> reported = [];
        IRenderedComponent<ActivityBar> cut = ctx.Render<ActivityBar>(p => p
            .Add(c => c.Selected, SidebarView.Chats)
            .Add(c => c.SelectedChanged, (SidebarView? view) => reported.Add(view)));

        await cut.Find("button[data-view='Chats']").ClickAsync(new());
        await cut.Find("button[data-view='Tasks']").ClickAsync(new());

        Assert.Equal([null, SidebarView.Tasks], reported);
    }

    /// <summary>Source pin: the shell's drawer opens on the default view, and Archived and Settings are in the Activity bar's bottom toolbar, not the Sidebar.</summary>
    [Fact]
    public void MainLayout_PutsTheFooterInTheActivityBarsBottomToolbar()
    {
        string layout = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));

        int bottom = layout.IndexOf("<Bottom>", StringComparison.Ordinal);
        int bottomEnd = layout.IndexOf("</Bottom>", StringComparison.Ordinal);
        int archived = layout.IndexOf("<ArchivedLink", StringComparison.Ordinal);
        int settings = layout.IndexOf("Href=\"/settings\"", StringComparison.Ordinal);
        Assert.True(bottom >= 0 && bottom < archived && archived < settings && settings < bottomEnd, "Archived and Settings must sit inside the Activity bar's Bottom slot");
        Assert.Contains("sidebarView = SidebarViews.Default", layout, StringComparison.Ordinal); // contains-ok: a source pin on one field initialiser in a file that holds much else
        Assert.Equal(SidebarView.Chats, SidebarViews.Default);
    }
}