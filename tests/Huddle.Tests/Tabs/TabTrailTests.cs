namespace Agency.Huddle.App.Tabs;

/// <summary>Pins <see cref="TabTrail"/>: the breadcrumb trail each kind of Tab page gets from its URL and title.</summary>
public sealed class TabTrailTests
{
    // Draws a trail as text: a crumb with an address is [Text](href), the rest is plain.
    private static string Draw(IReadOnlyList<TrailCrumb> trail) =>
        string.Join(" > ", trail.Select(static crumb => crumb.Href is null ? crumb.Text : $"[{crumb.Text}]({crumb.Href})"));

    /// <summary>Each routed page gets the area, then the page, with the last crumb the Tab's own title and every ancestor with a page of its own a link.</summary>
    [Theory]
    [InlineData("/", "Chats", "Chats")]
    [InlineData("/rooms/ab12", "Planning", "[Chats](/) > Planning")]
    [InlineData("/teams/new", "New team", "Teams > New team")]
    [InlineData("/teams/Core", "Core", "Teams > Core")]
    [InlineData("/teams/Core/tasks", "Core", "Teams > Core")]
    [InlineData("/teams/Core/projects/Alpha", "Alpha", "Teams > [Core](/teams/Core) > Alpha")]
    [InlineData("/teams/Core/projects/Alpha/files", "Alpha", "Teams > [Core](/teams/Core) > Alpha")]
    [InlineData("/teams/Core/setup", "Set up Core", "Teams > [Core](/teams/Core) > Setup")]
    [InlineData("/tasks", "Tasks", "Tasks")]
    [InlineData("/tasks/board", "board", "[Tasks](/tasks) > board")]
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

        Assert.Equal("Teams > [Sales & Ops](/teams/Sales%20%26%20Ops) > Launch", Draw(trail));
    }

    /// <summary>The last crumb carries the icon of the page it names, so a Project page ends in the Project icon.</summary>
    [Fact]
    public void Build_LastCrumb_CarriesThePagesIcon()
    {
        IReadOnlyList<TrailCrumb> trail = TabTrail.Build("/teams/Core/projects/Alpha", "Alpha");

        Assert.Equal("project", trail[^1].Icon);
        Assert.Null(trail[^1].Href);
    }

    /// <summary>A retitled Tab, such as a renamed Room, reaches the last crumb.</summary>
    [Fact]
    public void Build_UsesTheTabTitleForTheLastCrumb()
    {
        Assert.Equal("Renamed", TabTrail.Build("/rooms/ab12", "Renamed")[^1].Text);
    }
}