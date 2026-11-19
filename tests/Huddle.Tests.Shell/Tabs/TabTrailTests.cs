namespace Agency.Huddle.App.Tabs;

/// <summary>Pins <see cref="TabTrail"/>: the breadcrumb trail (the page's ancestors) each kind of Tab page gets from its URL.</summary>
public sealed class TabTrailTests
{
    // Draws a trail as text: a crumb with an address is [Text](href), the rest is plain.
    private static string Draw(IReadOnlyList<TrailCrumb> trail) =>
        string.Join(" > ", trail.Select(static crumb => crumb.Href is null ? crumb.Text : $"[{crumb.Text}]({crumb.Href})"));

    /// <summary>Each routed page gets its ancestors only, never itself: every page shows its name as its h1. A page with no ancestor has an empty trail; every ancestor with a page of its own is a link. A Project page is the exception: its trail ends in the Project as plain text.</summary>
    [Theory]
    [InlineData("/", "")]
    [InlineData("/rooms/ab12", "[Rooms](/)")]
    [InlineData("/teams/Core", "Teams")]
    [InlineData("/teams/Core/tasks", "Teams")]
    [InlineData("/teams/Core/projects/Alpha", "Teams > [Core](/teams/Core) > Alpha")]
    [InlineData("/teams/Core/projects/Alpha/files", "Teams > [Core](/teams/Core) > Alpha")]
    [InlineData("/teams/Core/projects/Alpha/tasks/board", "Teams > [Core](/teams/Core) > [Alpha](/teams/Core/projects/Alpha)")]
    [InlineData("/teams/Core/no-project/tasks/board", "Teams > [Core](/teams/Core) > No project")]
    [InlineData("/teams/Core/no-project/files", "Teams > [Core](/teams/Core)")]
    [InlineData("/teams/build", "Teams")]
    [InlineData("/tasks", "")]
    [InlineData("/tasks/my-tasks", "[Tasks](/tasks)")]
    [InlineData("/tasks/new", "[Tasks](/tasks)")]
    [InlineData("/tasks/item/7", "[Tasks](/tasks)")]
    [InlineData("/settings", "")]
    [InlineData("/settings/appearance", "")]
    [InlineData("/library?root=a&path=b.md", "")]
    [InlineData("/something/else", "")]
    public void Build_DrawsAncestorsOnly(string url, string expected)
    {
        Assert.Equal(expected, Draw(TabTrail.Build(url)));
    }

    /// <summary>A Room's trail is exactly one ancestor, "Rooms", matching the sidebar section, linking to the root.</summary>
    [Fact]
    public void Build_Room_TrailIsExactlyRooms()
    {
        IReadOnlyList<TrailCrumb> trail = TabTrail.Build("/rooms/ab12");

        TrailCrumb crumb = Assert.Single(trail);
        Assert.Equal("Rooms", crumb.Text);
        Assert.Equal("/", crumb.Href);
    }

    /// <summary>A Team name with spaces and punctuation is escaped in the Team crumb's address and shown unescaped in its text.</summary>
    [Fact]
    public void Build_TeamNameWithSpecialCharacters_EscapesTheAddress()
    {
        IReadOnlyList<TrailCrumb> trail = TabTrail.Build("/teams/Sales%20%26%20Ops/projects/Launch");

        Assert.Equal("Teams > [Sales & Ops](/teams/Sales%20%26%20Ops) > Launch", Draw(trail));
    }

    /// <summary>A page with no ancestor (Settings, a Task View's own page) has an empty trail, so the bar draws no one-crumb trail that repeats the h1.</summary>
    [Theory]
    [InlineData("/settings/profile")]
    [InlineData("/tasks")]
    public void Build_PageWithNoAncestor_IsEmpty(string url)
    {
        Assert.Empty(TabTrail.Build(url));
    }

    /// <summary>A Task View's trail is the ancestor "Tasks" and never the route's slug: the View's display name is the h1.</summary>
    [Fact]
    public void Build_TaskView_HoldsNoSlug()
    {
        IReadOnlyList<TrailCrumb> trail = TabTrail.Build("/tasks/platform-board");

        TrailCrumb only = Assert.Single(trail);
        Assert.Equal("Tasks", only.Text);
    }
}
