namespace Agency.Huddle.App.Tabs;

/// <summary>Pins <see cref="TabTrail"/>: the breadcrumb trail each kind of Tab page gets from its URL and title.</summary>
public sealed class TabTrailTests
{
    // Draws a trail as text: a crumb with an address is [Text](href), the rest is plain.
    private static string Draw(IReadOnlyList<TrailCrumb> trail) =>
        string.Join(" > ", trail.Select(static crumb => crumb.Href is null ? crumb.Text : $"[{crumb.Text}]({crumb.Href})"));

    /// <summary>Each routed page gets its ancestors, then (unless it draws its own name as its h1: a Room, Team or Project) its page, with the last crumb the Tab's own title and every ancestor with a page of its own a link.</summary>
    [Theory]
    [InlineData("/", "Chats", "Chats")]
    [InlineData("/rooms/ab12", "Planning", "[Chats](/)")]
    [InlineData("/teams/Core", "Core", "Teams")]
    [InlineData("/teams/Core/tasks", "Core", "Teams")]
    [InlineData("/teams/Core/projects/Alpha", "Alpha", "Teams > [Core](/teams/Core)")]
    [InlineData("/teams/Core/projects/Alpha/files", "Alpha", "Teams > [Core](/teams/Core)")]
    [InlineData("/teams/build", "Build a team", "Teams")]
    [InlineData("/tasks", "Tasks", "Tasks")]
    [InlineData("/tasks/my-tasks", "My Tasks", "[Tasks](/tasks) > My Tasks")]
    [InlineData("/tasks/new", "New task", "[Tasks](/tasks) > New task")]
    [InlineData("/tasks/item/7", "Task 7", "[Tasks](/tasks) > Task 7")]
    [InlineData("/teammates", "Teammates", "Teammates")]
    [InlineData("/settings", "Settings", "Settings")]
    [InlineData("/settings/appearance", "Settings", "[Settings](/settings) > Appearance")]
    [InlineData("/library?root=a&path=b.md", "Library", "Library")]
    [InlineData("/something/else", "else", "else")]
    public void Build_DrawsAreaThenPage(string url, string title, string expected)
    {
        Assert.Equal(expected, Draw(TabTrail.Build(url, title)));
    }

    /// <summary>A Team name with spaces and punctuation is escaped in the Team crumb's address and shown unescaped in its text.</summary>
    [Fact]
    public void Build_TeamNameWithSpecialCharacters_EscapesTheAddress()
    {
        IReadOnlyList<TrailCrumb> trail = TabTrail.Build("/teams/Sales%20%26%20Ops/projects/Launch", "Launch");

        Assert.Equal("Teams > [Sales & Ops](/teams/Sales%20%26%20Ops)", Draw(trail));
    }

    /// <summary>The last crumb carries the icon of the page it names, so a Task View page ends in the Tasks icon.</summary>
    [Fact]
    public void Build_LastCrumb_CarriesThePagesIcon()
    {
        IReadOnlyList<TrailCrumb> trail = TabTrail.Build("/tasks/my-tasks", "My Tasks");

        Assert.Equal("tasks", trail[^1].Icon);
        Assert.Null(trail[^1].Href);
    }

    /// <summary>A retitled Tab reaches the last crumb of a page that has no h1, such as a renamed Task View.</summary>
    [Fact]
    public void Build_UsesTheTabTitleForTheLastCrumb()
    {
        Assert.Equal("Renamed", TabTrail.Build("/tasks/my-tasks", "Renamed")[^1].Text);
    }

    /// <summary>A Room, Team or Project draws its own name as the h1, so its trail never repeats that name.</summary>
    [Theory]
    [InlineData("/rooms/ab12", "Grace")]
    [InlineData("/teams/Core", "Core")]
    [InlineData("/teams/Core/projects/Alpha", "Alpha")]
    public void Build_PageWithAnH1_DoesNotRepeatItsName(string url, string title)
    {
        Assert.DoesNotContain(TabTrail.Build(url, title), crumb => string.Equals(crumb.Text, title, StringComparison.Ordinal));
    }

    /// <summary>A Task View's trail shows the display name it is given and never the route's slug.</summary>
    [Fact]
    public void Build_TaskView_ShowsTheDisplayNameNotTheSlug()
    {
        IReadOnlyList<TrailCrumb> trail = TabTrail.Build("/tasks/platform-board", "Platform Board");

        Assert.Equal("Platform Board", trail[^1].Text);
        Assert.DoesNotContain(trail, crumb => crumb.Text.Contains('-', StringComparison.Ordinal));
    }
}