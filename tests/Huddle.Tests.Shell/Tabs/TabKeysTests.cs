namespace Agency.Huddle.App.Tabs;

public sealed class TabKeysTests
{
    /// <summary>Each routed page maps to the key the specification's table gives it.</summary>
    [Theory]
    [InlineData("/", "chat")]
    [InlineData("/rooms/ab12", "room:ab12")]
    [InlineData("/teams/Core", "team:core")]
    [InlineData("/teams/Core/tasks", "team:core")]
    [InlineData("/teams/Core/tasks/board", "team-tasks:core/board")]
    [InlineData("/teams/Core/no-project/tasks/board", "team-tasks:core/no-project/board")]
    [InlineData("/teams/Core/no-project/files", "team-tasks:core/no-project/\u0001files")]
    [InlineData("/teams/Core/projects/Alpha/tasks/board", "project-tasks:core/alpha/board")]
    [InlineData("/teams/Core/projects/Alpha", "project:core/alpha")]
    [InlineData("/teams/Core/projects/Alpha/files", "project:core/alpha")]
    [InlineData("/teams/build", "team-build")]
    [InlineData("/teams/build?type=product", "team-build")]
    [InlineData("/tasks", "tasks")]
    [InlineData("/tasks/board", "tasks:board")]
    [InlineData("/tasks/new", "task-new")]
    [InlineData("/tasks/item/7", "task:7")]
    [InlineData("/tasks/item/PLAT-0013", "task:PLAT-0013")]
    [InlineData("/tasks/item/PLAT-0013/edit", "task-edit:PLAT-0013")]
    [InlineData("/tasks/item/new/edit", "task-new-edit")]
    [InlineData("/tasks/item/new/edit?team=Platform&project=Alpha", "task-new-edit")]
    [InlineData("/tasks/item/PLAT-0013/copy", "task-copy:PLAT-0013")]
    [InlineData("/agents", "agents")]
    [InlineData("/settings", "settings")]
    [InlineData("/settings/appearance", "settings")]
    [InlineData("/library", "library")]
    [InlineData("/library?scopeRoot=a&scopePath=", "library")]
    [InlineData("/library?root=a&path=b.md", "library")]
    [InlineData("/something/else", "/something/else")]
    public void Describe_MapsUrlToKey(string url, string expectedKey)
    {
        Assert.Equal(expectedKey, TabKeys.Describe(url).Key);
    }

    /// <summary>An absolute address is reduced to its path, and a fragment is dropped.</summary>
    [Fact]
    public void ToRelative_AbsoluteWithFragment_KeepsPathAndQuery()
    {
        Assert.Equal("/rooms/ab?x=1", TabKeys.ToRelative("http://localhost:5000/rooms/ab?x=1#top"));
    }

    /// <summary>Only a single-slash path is a safe thing to remember.</summary>
    [Theory]
    [InlineData("/rooms/ab", true)]
    [InlineData("//evil", false)]
    [InlineData("https://evil", false)]
    [InlineData("\\evil", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSafeRelative_AcceptsOnlyPaths(string? url, bool expected)
    {
        Assert.Equal(expected, TabKeys.IsSafeRelative(url));
    }

    /// <summary>A Team's Tasks-folder address opens the Tasks page with its Team and View, not the Team page that <c>/teams/{Team}/{Tab}</c> would also reach.</summary>
    [Fact]
    public void Resolve_TeamViewAddress_OpensTheTasksPageNarrowedToTheTeam()
    {
        TabPageRoutes app = new(typeof(Agency.Huddle.App.ServiceCollectionExtensions).Assembly);

        TabPage? page = app.Resolve("/teams/Growth%20Team/tasks/my-tasks");

        Assert.NotNull(page);
        Assert.Equal(typeof(Agency.Huddle.App.Components.Pages.Tasks), page.Component);
        Assert.Equal("Growth Team", page.Parameters["Team"]);
        Assert.Equal("my-tasks", page.Parameters["ViewId"]);
    }

    /// <summary>A Project's Tasks-folder address opens the Tasks page with its Team, Project and View, not the Project page.</summary>
    [Fact]
    public void Resolve_ProjectViewAddress_OpensTheTasksPageNarrowedToTheProject()
    {
        TabPageRoutes app = new(typeof(Agency.Huddle.App.ServiceCollectionExtensions).Assembly);

        TabPage? page = app.Resolve("/teams/Growth%20Team/projects/Q4/tasks/my-tasks");

        Assert.NotNull(page);
        Assert.Equal(typeof(Agency.Huddle.App.Components.Pages.Tasks), page.Component);
        Assert.Equal("Growth Team", page.Parameters["Team"]);
        Assert.Equal("Q4", page.Parameters["Project"]);
        Assert.Equal("my-tasks", page.Parameters["ViewId"]);
    }
}
