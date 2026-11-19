using Agency.Huddle.Tests.Ui.Layout;

namespace Agency.Huddle.App.Tabs;

/// <summary>Pins how a Tab's URL becomes a page: the template that wins, and the values the page's parameters get.</summary>
public sealed class TabPageRoutesTests
{
    private static readonly TabPageRoutes Routes = StubPages.Routes();

    /// <summary>A route value reaches the component's parameter of the same name, whatever the case.</summary>
    [Fact]
    public void Resolve_RouteValue_FillsTheParameter()
    {
        TabPage? page = Routes.Resolve("/rooms/ab12");

        Assert.NotNull(page);
        Assert.Equal(typeof(StubRoomPage), page.Component);
        Assert.Equal("ab12", page.Parameters["RoomId"]);
    }

    /// <summary>Percent-encoded segments are decoded before they reach the page.</summary>
    [Fact]
    public void Resolve_EncodedSegment_IsDecoded()
    {
        TabPage? page = Routes.Resolve("/teams/Design%20Guild");

        Assert.NotNull(page);
        Assert.Equal("Design Guild", page.Parameters["Team"]);
    }

    /// <summary>A literal template beats a parameter template that would also match.</summary>
    [Theory]
    [InlineData("/tasks/new", typeof(StubTasksNewPage))]
    [InlineData("/tasks/all", typeof(StubTasksViewPage))]
    [InlineData("/teams/new", typeof(StubTeamNewPage))]
    [InlineData("/teams/Design", typeof(StubTeamPage))]
    public void Resolve_PrefersTheMostLiteralTemplate(string url, Type expected)
    {
        TabPage? page = Routes.Resolve(url);

        Assert.NotNull(page);
        Assert.Equal(expected, page.Component);
    }

    /// <summary>Query values fill the parameters the component declares, which is how the Library page gets its file.</summary>
    [Fact]
    public void Resolve_QueryValues_FillMatchingParameters()
    {
        TabPage? page = Routes.Resolve("/library?root=notes&path=a%2Fb.md&other=ignored");

        Assert.NotNull(page);
        Assert.Equal(typeof(StubLibraryPage), page.Component);
        Assert.Equal("notes", page.Parameters["Root"]);
        Assert.Equal("a/b.md", page.Parameters["Path"]);
        Assert.False(page.Parameters.ContainsKey("other"));
    }

    /// <summary>In the app's own assembly the edit address resolves to the Task edit page, and the plain item address still to the Tasks page, with the id filled in (read view spec, "What changes in code").</summary>
    [Fact]
    public void Resolve_AppAssembly_TaskEditRouteBeatsTheItemRoute()
    {
        TabPageRoutes app = new(typeof(Agency.Huddle.App.ServiceCollectionExtensions).Assembly);

        TabPage? edit = app.Resolve("/tasks/item/PLAT-0013/edit");
        TabPage? item = app.Resolve("/tasks/item/PLAT-0013");

        Assert.NotNull(edit);
        Assert.Equal(typeof(Agency.Huddle.App.Components.Pages.TaskEditPage), edit.Component);
        Assert.Equal("PLAT-0013", edit.Parameters["TaskIdText"]);
        Assert.NotNull(item);
        Assert.NotEqual(typeof(Agency.Huddle.App.Components.Pages.TaskEditPage), item.Component);
        Assert.Equal("PLAT-0013", item.Parameters["TaskIdText"]);
    }

    /// <summary>The new-Task and copy addresses resolve to the same edit page, with the id segment and the Team and Project query values filled in.</summary>
    [Fact]
    public void Resolve_AppAssembly_NewAndCopyAddressesReachTheEditPage()
    {
        TabPageRoutes app = new(typeof(Agency.Huddle.App.ServiceCollectionExtensions).Assembly);

        TabPage? created = app.Resolve("/tasks/item/new/edit?team=Platform&project=Alpha");
        TabPage? copy = app.Resolve("/tasks/item/PLAT-0013/copy");

        Assert.NotNull(created);
        Assert.Equal(typeof(Agency.Huddle.App.Components.Pages.TaskEditPage), created.Component);
        Assert.Equal("new", created.Parameters["TaskIdText"]);
        Assert.Equal("Platform", created.Parameters["Team"]);
        Assert.Equal("Alpha", created.Parameters["Project"]);
        Assert.NotNull(copy);
        Assert.Equal(typeof(Agency.Huddle.App.Components.Pages.TaskEditPage), copy.Component);
        Assert.Equal("PLAT-0013", copy.Parameters["TaskIdText"]);
    }

    /// <summary>An address with no template resolves to nothing, so the pane can say so.</summary>
    [Theory]
    [InlineData("/nowhere")]
    [InlineData("/rooms")]
    [InlineData("/rooms/a/b/c")]
    public void Resolve_NoTemplate_ReturnsNull(string url)
    {
        Assert.Null(Routes.Resolve(url));
    }

    /// <summary>Every <c>@page</c> in the app resolves to its own component, so a stored Tab opens the page it was saved for.</summary>
    [Theory]
    [InlineData("/", "Chat")]
    [InlineData("/rooms/ab12", "Chat")]
    [InlineData("/settings", "Settings")]
    [InlineData("/settings/appearance", "Settings")]
    [InlineData("/library?root=r&path=p", "Library")]
    [InlineData("/tasks", "Tasks")]
    [InlineData("/tasks/new", "Tasks")]
    [InlineData("/tasks/item/12", "Tasks")]
    [InlineData("/teams/build", "BuildTeam")]
    [InlineData("/teams/Design", "TeamPage")]
    [InlineData("/teams/Design/tasks", "TeamPage")]
    [InlineData("/teams/Design/projects/Site/overview", "TeamPage")]
    public void Resolve_TheAppsOwnPages_FindTheirComponent(string url, string component)
    {
        TabPageRoutes appRoutes = new(typeof(TabService).Assembly);

        TabPage? page = appRoutes.Resolve(url);

        Assert.NotNull(page);
        Assert.Equal(component, page.Component.Name);
    }

    /// <summary>The Library page's four query values land on parameters it declares, since it no longer reads the browser URL.</summary>
    [Fact]
    public void Resolve_TheAppsLibraryPage_GetsItsQueryAsParameters()
    {
        TabPageRoutes appRoutes = new(typeof(TabService).Assembly);

        TabPage? page = appRoutes.Resolve("/library?scopeRoot=notes&scopePath=docs&root=notes&path=a.md");

        Assert.NotNull(page);
        Assert.Equal("notes", page.Parameters["ScopeRoot"]);
        Assert.Equal("docs", page.Parameters["ScopePath"]);
        Assert.Equal("notes", page.Parameters["Root"]);
        Assert.Equal("a.md", page.Parameters["Path"]);
    }

    /// <summary>The context's path drops the query.</summary>
    [Fact]
    public void TabPageContext_Path_DropsTheQuery()
    {
        Assert.Equal("/library", new TabPageContext("/library?root=a", "t1").Path);
        Assert.Equal("/tasks/new", new TabPageContext("/tasks/new", "t1").Path);
    }
}
