using Agency.Huddle.App.Components.Layout;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Pins that the Activity bar (<c>S5</c>) lights one tile, the last one clicked, when a page tile's page (Feed, Archived,
/// Settings) is the active Tab while a Sidebar view is shown: the bug was the Feed and Library tiles both lit.
/// </summary>
public sealed class ActivityBarHighlightTests
{
    /// <summary>The page tiles' addresses are recognised by their first segment, in any case and with a tail; others are not.</summary>
    /// <param name="url">The active Tab's address.</param>
    /// <param name="expected">Whether it is a page tile's page.</param>
    [Theory]
    [InlineData("/feed", true)]
    [InlineData("/Feed?team=x", true)]
    [InlineData("/archived/teams", true)]
    [InlineData("/settings/profile", true)]
    [InlineData("/library?root=Teams", false)]
    [InlineData("/feedback", false)]
    [InlineData("/", false)]
    [InlineData(null, false)]
    public void IsPageTileAddress_MatchesOnlyFeedArchivedAndSettings(string? url, bool expected)
    {
        Assert.Equal(expected, SidebarViews.IsPageTileAddress(url));
    }

    /// <summary>With the Feed open, clicking a view tile lights the view and dims the Feed tile: never both.</summary>
    [Fact]
    public void ViewClicked_OnTheFeed_LightsTheViewOnly()
    {
        ActivityBarHighlight highlight = new();
        _ = highlight.ActiveTabSeen("/feed");
        Assert.True(highlight.PageTileLit);
        Assert.False(highlight.ViewTileLit("/feed"));

        highlight.ViewClicked(collapsed: false);

        Assert.False(highlight.PageTileLit);
        Assert.True(highlight.ViewTileLit("/feed"));
    }

    /// <summary>After a view click, clicking the Feed tile again lights the Feed and dims the view.</summary>
    [Fact]
    public void PageTileClicked_AfterAView_LightsThePageTileOnly()
    {
        ActivityBarHighlight highlight = new();
        _ = highlight.ActiveTabSeen("/feed");
        highlight.ViewClicked(collapsed: false);

        highlight.PageTileClicked();

        Assert.True(highlight.PageTileLit);
        Assert.False(highlight.ViewTileLit("/feed"));
    }

    /// <summary>Bringing a page tile's page forward from another Tab lights its tile; a later change on the same page does not undo a view click.</summary>
    [Fact]
    public void ActiveTabSeen_MovingToTheFeed_LightsIt_ButStayingThereDoesNot()
    {
        ActivityBarHighlight highlight = new();
        _ = highlight.ActiveTabSeen("/tasks/all");
        highlight.ViewClicked(collapsed: false);

        Assert.True(highlight.ActiveTabSeen("/feed"));
        Assert.True(highlight.PageTileLit);

        highlight.ViewClicked(collapsed: false);
        Assert.False(highlight.ActiveTabSeen("/feed"));
        Assert.False(highlight.PageTileLit);
    }

    /// <summary>On a page that is not a page tile's, the view's tile is lit whatever was clicked last.</summary>
    [Fact]
    public void ViewTileLit_OffAPageTilesPage_IsAlwaysTrue()
    {
        ActivityBarHighlight highlight = new();

        highlight.PageTileClicked();

        Assert.True(highlight.ViewTileLit("/library?root=Teams"));
    }

    /// <summary>Collapsing the Sidebar leaves no view to light, so the open page's tile lights again.</summary>
    [Fact]
    public void ViewClicked_Collapsing_LightsThePageTile()
    {
        ActivityBarHighlight highlight = new();
        highlight.ViewClicked(collapsed: false);

        highlight.ViewClicked(collapsed: true);

        Assert.True(highlight.PageTileLit);
    }
}
