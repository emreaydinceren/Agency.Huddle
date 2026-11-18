using System.Text.RegularExpressions;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Visual audit round 3, package SHELL: the Activity bar follows the focused page's area (B3a), the group header, the
/// action rows and the Needs you row sit on the 32px row (B3b, B3c, B3e), the selected row and the Activity bar's tiles wear
/// the inset shape (A5), and the dialogs and menus the shell owns follow the round-3 idioms (B10). A browser is not
/// rendered here, so the stylesheet and markup are pinned as source text.
/// </summary>
public sealed partial class R3ShellTests
{
    /// <summary>An address belongs to the area named by its first path segment; the home page and unknown addresses belong to none.</summary>
    /// <param name="url">The Tab's address.</param>
    /// <param name="expected">The area, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("/library", "library")]
    [InlineData("/library?root=notes", "library")]
    [InlineData("/agents", "agents")]
    [InlineData("/teams/Business", "teams")]
    [InlineData("/teams/Business/projects/Q4", "teams")]
    [InlineData("/tasks/my-tasks", "tasks")]
    [InlineData("/settings/profile", "settings")]
    [InlineData("/rooms/ab12", "rooms")]
    [InlineData("/scheduled", "scheduled")]
    [InlineData("/scheduled/job-7f3a2c", "scheduled")]
    [InlineData("/", null)]
    [InlineData("", null)]
    [InlineData("/nowhere", null)]
    public void AreaOf_NamesTheAreaOfAnAddress(string url, string? expected)
    {
        Assert.Equal(expected, SidebarViews.AreaOf(url));
    }

    /// <summary>Entering an area shows that area's view, but a Team page keeps a Team shortcut's view; Settings collapses the Sidebar; a Room keeps the current view and opens Chats from a collapsed Sidebar.</summary>
    /// <param name="area">The area entered.</param>
    /// <param name="current">The view shown before.</param>
    /// <param name="expected">The view shown after, or <see langword="null"/> for a collapsed Sidebar.</param>
    [Theory]
    [InlineData("library", SidebarView.Rooms, SidebarView.Library)]
    [InlineData("agents", SidebarView.Rooms, SidebarView.Agents)]
    [InlineData("teams", SidebarView.Rooms, SidebarView.Teams)]
    [InlineData("teams", SidebarView.Team, SidebarView.Team)]
    [InlineData("rooms", SidebarView.Team, SidebarView.Team)]
    [InlineData("tasks", SidebarView.Library, SidebarView.Tasks)]
    [InlineData("scheduled", SidebarView.Rooms, SidebarView.Scheduled)]
    [InlineData("settings", SidebarView.Rooms, null)]
    [InlineData("rooms", SidebarView.Teams, SidebarView.Teams)]
    [InlineData("rooms", null, SidebarView.Rooms)]
    public void ViewForArea_ShowsTheAreasView(string area, SidebarView? current, SidebarView? expected)
    {
        Assert.Equal(expected, SidebarViews.ViewForArea(area, current));
    }

    /// <summary>MainLayout follows the focused pane through <c>TabService.Changed</c> and unsubscribes on dispose, and only when the area changes, so a view the Human picked stays.</summary>
    [Fact]
    public void MainLayout_FollowsTheFocusedArea_OnlyWhenTheAreaChanges()
    {
        string layout = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));

        Assert.Contains("this.Tabs.Changed += this.OnTabsChanged;", layout, StringComparison.Ordinal); // contains-ok: a source pin on one subscription in a file that holds much else
        Assert.Contains("this.Tabs.Changed -= this.OnTabsChanged;", layout, StringComparison.Ordinal); // contains-ok: a source pin on one unsubscription in a file that holds much else
        Assert.Contains("string.Equals(area, this.followedArea, StringComparison.Ordinal)", layout, StringComparison.Ordinal); // contains-ok: a source pin on the change-of-area guard
    }

    /// <summary>A group header is 32px like a row (B3b), and "Show all" is 13px like "New room" (B3c).</summary>
    [Fact]
    public void AppCss_GroupHeaderAndShowAll_AreOneRowHigh_AndThirteenPixels()
    {
        Assert.Matches(@"height:\s*var\(--nav-row-height\)", R3ShellTests.DeclarationsFor(".mud-nav-group > .mud-nav-link"));
        Assert.Matches(@"font-size:\s*var\(--mud-typography-body2-size\)", R3ShellTests.DeclarationsFor(".sidebar-show-all .mud-nav-link-text"));
    }

    /// <summary>The sidebar's rows are inset 4px and rounded at 0.75x the base radius, and the Activity bar's buttons are 40px tiles with the same shape (A5).</summary>
    [Fact]
    public void AppCss_SidebarRowsAndActivityBar_WearTheInsetShape()
    {
        string row = R3ShellTests.DeclarationsFor(".mud-drawer .mud-navmenu .mud-nav-link");
        Assert.Matches(@"margin-inline:\s*var\(--nav-inset\)", row);
        Assert.Matches(@"--nav-inset:\s*4px", CssText.AppCss(CssNewlines.AsIs));
        Assert.Matches(@"border-radius:\s*var\(--hu-radius-control\)", row);

        string tile = R3ShellTests.DeclarationsFor(".activity-bar-item.mud-icon-button");
        Assert.Matches(@"width:\s*40px", tile);
        Assert.Matches(@"height:\s*40px", tile);
        Assert.Matches(@"border-radius:\s*var\(--hu-radius-control\)", tile);
    }

    /// <summary>
    /// The Needs you block is flat, its outline transparent (side-by-side audit, 2026-10-30, G03, replacing B3e's Edge
    /// outline), and its row is one 32px line whose time trails (B3e).
    /// </summary>
    [Fact]
    public void AppCss_NeedsYou_IsFlat_AndOneRowHigh()
    {
        Assert.Matches(@"border-color:\s*transparent", R3ShellTests.DeclarationsFor(".needs-you-card.mud-paper-outlined"));
        Assert.Matches(@"min-height:\s*var\(--nav-row-height\)", R3ShellTests.DeclarationsFor(".sidebar-views .needs-you-row.needs-you-row .mud-nav-link"));
        Assert.DoesNotMatch(@"flex-direction:\s*column", R3ShellTests.DeclarationsFor(".needs-you-text"));
    }

    /// <summary>The Archived page, which replaced the Archived rooms dialog, offers Restore as its one filled primary button and a chat's Delete as Error text, asked first.</summary>
    [Fact]
    public void ArchivedPage_RestoreIsFilledPrimary_AndDeleteIsErrorTextAskedFirst()
    {
        string source = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "ArchivedPage.razor"));

        // The attributes hold lambdas ("=>"), so each pattern spans them lazily rather than stopping at the first '>'.
        Assert.Matches(@"<ActionButton Role=""ButtonRole\.Primary""[^<]*?Class=""archived-restore""[^<]*?>Restore</ActionButton>", source);
        Assert.Matches(@"<ActionButton Role=""ButtonRole\.DangerQuiet""[^<]*?Class=""archived-delete""[^<]*?>Delete…</ActionButton>", source);
        Assert.Matches(@"<InlineConfirm Class=""archived-delete-confirm""", source);
    }

    /// <summary>New chat is a floating panel with Cancel and Start chat (B10), not an inline full-width block.</summary>
    [Fact]
    public void NewChat_IsAFloatingPanel_WithCancelAndStart()
    {
        string source = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "NewChat.razor"));

        Assert.Matches(@"class=""new-chat-panel floating-panel""", source);
        Assert.Matches(@"new-chat-cancel[^>]*>Cancel</ActionButton>", source);
        Assert.Matches(@"new-chat-start[^>]*>Open room</ActionButton>", source);
        Assert.DoesNotMatch(@"FullWidth=""true""", source);
    }

    /// <summary>The sidebar's menus (Room row, Team) open dialogs after closing, carry no activator tooltip, and name their target (B10e, B10f, B10i).</summary>
    [Fact]
    public void SidebarMenus_FollowTheMenuIdiom()
    {
        string room = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "RoomNavRow.razor"));
        string teams = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Teams", "TeamsNav.razor"));

        Assert.DoesNotContain("MudTooltip Text=\"Room actions\"", room, StringComparison.Ordinal);
        Assert.DoesNotContain("MudTooltip Text=\"Team actions\"", teams, StringComparison.Ordinal);
        Assert.Contains("MenuDialogs.ActivatorLabel(this.Room.Name)", room, StringComparison.Ordinal); // contains-ok: a source pin on the activator's label
        Assert.Contains("MenuDialogs.ActivatorLabel(team.Name)", teams, StringComparison.Ordinal); // contains-ok: a source pin on the activator's label
        Assert.Contains("MenuDialogs.AfterClosingAsync(menu,", teams, StringComparison.Ordinal); // contains-ok: a source pin on the menu-closes-first call
        Assert.Matches(@"<MudDivider />\s*<MudMenuItem[^>]*menu-item-destructive[^>]*>Delete</MudMenuItem>", room);
        Assert.Contains("Narrow=\"true\" Prompt=\"Delete?\"", room, StringComparison.Ordinal); // contains-ok: a source pin on the sidebar's narrow confirm
    }

    /// <summary>The declarations of every app.css rule that lists <paramref name="selector"/> among its comma-separated selectors, joined; fails when no rule does.</summary>
    /// <param name="selector">The exact selector.</param>
    private static string DeclarationsFor(string selector)
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        List<string> bodies = [];
        foreach (Match match in RulePattern().Matches(css))
        {
            string[] selectors = match.Groups["selectors"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (selectors.Contains(selector, StringComparer.Ordinal))
            {
                bodies.Add(match.Groups["body"].Value);
            }
        }

        Assert.True(bodies.Count > 0, $"app.css has no rule '{selector}'.");
        return string.Join("\n", bodies);
    }

    [GeneratedRegex(@"(?m)^(?<selectors>[^\s/}@][^{};]*?)\s*\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex RulePattern();
}
