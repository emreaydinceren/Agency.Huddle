using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Spec §6.6 and §8.3 for <c>TeamPage</c> with the three tab components replaced by bUnit stubs
/// that capture their parameters: the four route templates, the tabs each page offers under the two
/// feature flags and their defaults, the unknown-tab fallback, the route-driven tab navigation with
/// escaped segments, the unknown Team and Project alerts, the breadcrumbs, the display spelling handed
/// to the tabs, the per-tab search text and its reset, and the page's <see cref="ITeamCatalog.Changed"/>
/// subscription. The over-HTTP rows (routes, escaping, the page title) belong to Task 7.13b.
/// </summary>
public sealed class TeamPageTests
{
    private const string TabHeaderSelector = ".team-page-tabs .mud-tab";

    private const string AlertSelector = ".team-page-notice[role='alert']";

    private static readonly char[] WhitespaceChars = [' ', '\r', '\n', '\t'];

    /// <summary>The page declares exactly the four route templates of Spec §6.6, with <c>projects</c> a literal segment.</summary>
    [Fact]
    public void Routes_AreTheFourTemplatesOfTheSpec()
    {
        string[] templates = [.. typeof(TeamPage)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .OfType<RouteAttribute>()
            .Select(route => route.Template)
            .Order(StringComparer.Ordinal)];

        string[] expected =
        [
            "/teams/{Team}",
            "/teams/{Team}/projects/{Project}",
            "/teams/{Team}/projects/{Project}/{Tab}",
            "/teams/{Team}/{Tab}",
        ];
        Assert.Equal(expected, templates);
    }

    /// <summary>The Members tab stub receives the catalog's own Team on a Team page, and the page renders no error alert.</summary>
    [Fact]
    public async Task TeamPage_MembersTab_ReceivesTheCatalogTeam()
    {
        FakeTeamCatalog catalog = Catalog();
        await using MudBunitContext ctx = NewContext(catalog);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");

        TeamSummary? team = cut.FindComponent<Stub<TeamMembers>>().Instance.Parameters.Get(p => p.Team);
        Assert.Same(catalog.Teams[0], team);
        Assert.Equal("Business", team?.Name);
        Assert.Empty(cut.FindAll(AlertSelector));
    }

    /// <summary>Each feature-flag combination offers exactly the tabs of Spec §6.6, in order, and renders only the default tab's component.</summary>
    /// <param name="libraryEnabled"><c>Library:Enabled</c>.</param>
    /// <param name="tasksEnabled"><c>Tasks:Enabled</c>.</param>
    /// <param name="project">The Project, or <see langword="null"/> for a Team page.</param>
    /// <param name="expectedHeaders">The tab headers, comma-joined.</param>
    /// <param name="expectedStub">The one tab component that renders.</param>
    [Theory]
    [InlineData(true, true, null, "Members,Files,Tasks", nameof(TeamMembers))]
    [InlineData(true, true, "Marketing Project", "Files,Tasks", nameof(TeamFilesTab))]
    [InlineData(false, true, null, "Members,Tasks", nameof(TeamMembers))]
    [InlineData(false, true, "Marketing Project", "Tasks", nameof(TeamTasksTab))]
    [InlineData(true, false, null, "Members,Files", nameof(TeamMembers))]
    [InlineData(true, false, "Marketing Project", "Files", nameof(TeamFilesTab))]
    [InlineData(false, false, null, "Members", nameof(TeamMembers))]
    public async Task Tabs_FollowTheFlags_AndOnlyTheDefaultTabRenders(
        bool libraryEnabled,
        bool tasksEnabled,
        string? project,
        string expectedHeaders,
        string expectedStub)
    {
        await using MudBunitContext ctx = NewContext(Catalog(), libraryEnabled, tasksEnabled);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", project);

        Assert.Equal(expectedHeaders, string.Join(',', TabHeaders(cut)));
        Assert.Equal([expectedStub], RenderedTabs(cut));
    }

    /// <summary>A Project page with both features off shows the Info alert and no tabs and no tab component.</summary>
    [Fact]
    public async Task ProjectPage_BothFlagsOff_ShowsTheTurnedOffAlert()
    {
        await using MudBunitContext ctx = NewContext(Catalog(), libraryEnabled: false, tasksEnabled: false);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", "Marketing Project");

        Assert.Equal("Files and Tasks are turned off in this installation.", cut.Find($"{AlertSelector} .team-page-notice-text").TextContent.Trim());
        Assert.Equal(Severity.Info, cut.FindComponent<MudAlert>().Instance.Severity);
        Assert.Empty(TabHeaders(cut));
        Assert.Empty(RenderedTabs(cut));
    }

    /// <summary>A missing, unknown or disabled <c>Tab</c> falls back to the page's default tab without throwing; a known one is matched ignoring case.</summary>
    /// <param name="project">The Project, or <see langword="null"/> for a Team page.</param>
    /// <param name="tab">The <c>Tab</c> route value.</param>
    /// <param name="libraryEnabled"><c>Library:Enabled</c>.</param>
    /// <param name="expectedStub">The one tab component that renders.</param>
    [Theory]
    [InlineData(null, "nonsense", true, nameof(TeamMembers))]
    [InlineData("Marketing Project", "nonsense", true, nameof(TeamFilesTab))]
    [InlineData(null, "FILES", true, nameof(TeamFilesTab))]
    [InlineData(null, "files", false, nameof(TeamMembers))]
    [InlineData("Marketing Project", "files", false, nameof(TeamTasksTab))]
    [InlineData("Marketing Project", "members", true, nameof(TeamFilesTab))]
    public async Task Tab_UnknownOrDisabled_FallsBackToTheDefault(string? project, string tab, bool libraryEnabled, string expectedStub)
    {
        await using MudBunitContext ctx = NewContext(Catalog(), libraryEnabled);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", project, tab);

        Assert.Equal([expectedStub], RenderedTabs(cut));
    }

    /// <summary>The route's <c>Tab</c> picks the tab component, and Files and Tasks receive the Team and Project (spelt as the catalog spells them).</summary>
    /// <param name="tab">The <c>Tab</c> route value.</param>
    /// <param name="project">The Project, or <see langword="null"/> for a Team page.</param>
    [Theory]
    [InlineData("files", null)]
    [InlineData("tasks", null)]
    [InlineData("files", "Marketing Project")]
    [InlineData("tasks", "Marketing Project")]
    public async Task Tab_FilesOrTasks_ReceivesTheTeamAndProject(string tab, string? project)
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", project, tab);

        string expectedStub = string.Equals(tab, "files", StringComparison.Ordinal) ? nameof(TeamFilesTab) : nameof(TeamTasksTab);
        Assert.Equal([expectedStub], RenderedTabs(cut));
        (string? team, string? actualProject) = string.Equals(tab, "files", StringComparison.Ordinal)
            ? (FilesParameters(cut).Get(p => p.Team), FilesParameters(cut).Get(p => p.Project))
            : (TasksParameters(cut).Get(p => p.Team), TasksParameters(cut).Get(p => p.Project));
        Assert.Equal("Business", team);
        Assert.Equal(project, actualProject);
    }

    /// <summary>A lower-case route spelling reaches the tabs, the breadcrumbs and the Members stub as the catalog's own spelling.</summary>
    [Fact]
    public async Task Route_InAnotherCase_FeedsTheDisplaySpellingToTheTabs()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> members = RenderPage(ctx, "business");
        Assert.Equal("Business", members.FindComponent<Stub<TeamMembers>>().Instance.Parameters.Get(p => p.Team)?.Name);
        Assert.Equal("Business", Words(members.Find(".team-page-breadcrumbs").TextContent));

        IRenderedComponent<TeamPage> files = RenderPage(ctx, "business", "marketing PROJECT", "files");
        Assert.Equal("Business", FilesParameters(files).Get(p => p.Team));
        Assert.Equal("Marketing Project", FilesParameters(files).Get(p => p.Project));
        Assert.Equal("Business › Marketing Project", Words(files.Find(".team-page-breadcrumbs").TextContent));

        IRenderedComponent<TeamPage> tasks = RenderPage(ctx, "BUSINESS", "marketing project", "tasks");
        Assert.Equal("Business", TasksParameters(tasks).Get(p => p.Team));
        Assert.Equal("Marketing Project", TasksParameters(tasks).Get(p => p.Project));
    }

    /// <summary>The Team page's breadcrumbs name the Team; the Project page's read "Team › Project" with the Team a link to its page.</summary>
    [Fact]
    public async Task Breadcrumbs_NameTheTeamAndProject()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> team = RenderPage(ctx, "Business");
        IRenderedComponent<TeamPage> project = RenderPage(ctx, "Business", "Marketing Project");

        Assert.Equal("Business", Words(team.Find(".team-page-breadcrumbs").TextContent));
        Assert.Equal("Business › Marketing Project", Words(project.Find(".team-page-breadcrumbs").TextContent));
        Assert.Equal("Business", project.Find(".team-page-breadcrumbs a[href='/teams/Business']").TextContent.Trim());
    }

    /// <summary>Clicking a tab header navigates to the tab's route with escaped segments and never changes the page's own <c>Tab</c> parameter.</summary>
    /// <param name="team">The Team route value.</param>
    /// <param name="project">The Project route value, or <see langword="null"/>.</param>
    /// <param name="startTab">The <c>Tab</c> route value the page starts on, or <see langword="null"/>.</param>
    /// <param name="headerIndex">The header clicked.</param>
    /// <param name="expectedPath">The path navigated to.</param>
    [Theory]
    [InlineData("Business", null, null, 1, "/teams/Business/files")]
    [InlineData("Business", null, null, 2, "/teams/Business/tasks")]
    [InlineData("Business", null, "files", 0, "/teams/Business/members")]
    [InlineData("Business", "Marketing Project", null, 1, "/teams/Business/projects/Marketing%20Project/tasks")]
    [InlineData("Sales & Ops", "Q4 #1", "tasks", 0, "/teams/Sales%20%26%20Ops/projects/Q4%20%231/files")]
    [InlineData("Sales & Ops", null, null, 1, "/teams/Sales%20%26%20Ops/files")]
    public async Task TabClick_NavigatesToTheEscapedTabRoute_AndTheRouteStaysTheDriver(
        string team,
        string? project,
        string? startTab,
        int headerIndex,
        string expectedPath)
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, team, project, startTab);

        cut.FindAll(TabHeaderSelector)[headerIndex].Click();

        Assert.Equal($"http://localhost{expectedPath}", nav.Uri);
        Assert.Equal(startTab, cut.Instance.Tab);
        Assert.Equal(team, cut.Instance.Team);
        Assert.Equal(project, cut.Instance.Project);
    }

    /// <summary>A Tab parameter arriving from the route switches the rendered tab component.</summary>
    [Fact]
    public async Task RouteChange_ToAnotherTab_SwitchesTheRenderedTabComponent()
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");
        Assert.Equal([nameof(TeamMembers)], RenderedTabs(cut));

        cut.Render(p => p.Add(x => x.Tab, "tasks"));

        Assert.Equal([nameof(TeamTasksTab)], RenderedTabs(cut));
    }

    /// <summary>An unknown Team shows the Warning alert naming it with a link to the Teammates page, and renders no tabs.</summary>
    /// <param name="project">A Project route value, or <see langword="null"/>: an unknown Team wins either way.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("Marketing Project")]
    public async Task UnknownTeam_ShowsTheAlertWithTheTeammatesLink(string? project)
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Nope", project);

        Assert.Equal("There is no Team named \"Nope\".", cut.Find($"{AlertSelector} .team-page-notice-text").TextContent.Trim());
        Assert.Equal(Severity.Warning, cut.FindComponent<MudAlert>().Instance.Severity);
        AngleSharp.Dom.IElement link = cut.Find($"{AlertSelector} a.team-page-notice-link");
        Assert.Equal("/teammates", link.GetAttribute("href"));
        Assert.Equal("Teammates", link.TextContent.Trim());
        Assert.Empty(TabHeaders(cut));
        Assert.Empty(RenderedTabs(cut));
    }

    /// <summary>An unknown Project shows the Warning alert naming it and the Team, linking to the (escaped) Team page, and renders no tabs.</summary>
    /// <param name="team">The Team route value.</param>
    /// <param name="expectedText">The alert text.</param>
    /// <param name="expectedHref">The Team link's href.</param>
    /// <param name="expectedLinkText">The Team link's text.</param>
    [Theory]
    [InlineData("business", "There is no Project named \"Nope\" in Team \"Business\".", "/teams/Business", "Business")]
    [InlineData("Sales & Ops", "There is no Project named \"Nope\" in Team \"Sales & Ops\".", "/teams/Sales%20%26%20Ops", "Sales & Ops")]
    public async Task UnknownProject_ShowsTheAlertWithTheTeamLink(string team, string expectedText, string expectedHref, string expectedLinkText)
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, team, "Nope");

        Assert.Equal(expectedText, cut.Find($"{AlertSelector} .team-page-notice-text").TextContent.Trim());
        Assert.Equal(Severity.Warning, cut.FindComponent<MudAlert>().Instance.Severity);
        AngleSharp.Dom.IElement link = cut.Find($"{AlertSelector} a.team-page-notice-link");
        Assert.Equal(expectedHref, link.GetAttribute("href"));
        Assert.Equal(expectedLinkText, link.TextContent.Trim());
        Assert.Empty(TabHeaders(cut));
        Assert.Empty(RenderedTabs(cut));
    }

    /// <summary>T14: a Team with a folder and no members renders the Members tab with that Team, and no error.</summary>
    [Fact]
    public async Task TeamWithAFolderAndNoMembers_RendersTheMembersTab()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", members: [], hasFolder: true)] };
        await using MudBunitContext ctx = NewContext(catalog);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");

        TeamSummary? team = cut.FindComponent<Stub<TeamMembers>>().Instance.Parameters.Get(p => p.Team);
        Assert.Equal("Business", team?.Name);
        Assert.Empty(team?.Members ?? ["unreachable"]);
        Assert.Empty(cut.FindAll(AlertSelector));
    }

    /// <summary>T15: a label-only Team (no folder) renders the page and its Files tab, and no error.</summary>
    [Fact]
    public async Task LabelOnlyTeam_RendersThePageAndTheFilesTab()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", hasFolder: false)] };
        await using MudBunitContext ctx = NewContext(catalog);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", tab: "files");

        Assert.Equal([nameof(TeamFilesTab)], RenderedTabs(cut));
        Assert.Equal("Business", FilesParameters(cut).Get(p => p.Team));
        Assert.Equal("Members,Files,Tasks", string.Join(',', TabHeaders(cut)));
        Assert.Empty(cut.FindAll(AlertSelector));
    }

    /// <summary>Each tab keeps its own search text while the route switches tabs on the same Team, and the text is not carried across tabs.</summary>
    [Fact]
    public async Task Search_IsKeptPerTab_WhileSwitchingTabs()
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");

        await cut.InvokeAsync(() => MembersParameters(cut).Get(p => p.SearchChanged).InvokeAsync("ann"));
        Assert.Equal("ann", MembersParameters(cut).Get(p => p.Search));

        cut.Render(p => p.Add(x => x.Tab, "files"));
        Assert.Null(FilesParameters(cut).Get(p => p.Search));
        await cut.InvokeAsync(() => FilesParameters(cut).Get(p => p.SearchChanged).InvokeAsync("brief"));
        Assert.Equal("brief", FilesParameters(cut).Get(p => p.Search));

        cut.Render(p => p.Add(x => x.Tab, "tasks"));
        Assert.Null(TasksParameters(cut).Get(p => p.Search));

        cut.Render(p => p.Add(x => x.Tab, (string?)null));
        Assert.Equal("ann", MembersParameters(cut).Get(p => p.Search));
        cut.Render(p => p.Add(x => x.Tab, "files"));
        Assert.Equal("brief", FilesParameters(cut).Get(p => p.Search));
    }

    /// <summary>The search text is reset when the Team parameter changes, and not restored when it changes back.</summary>
    [Fact]
    public async Task Search_IsReset_WhenTheTeamChanges()
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");
        await cut.InvokeAsync(() => MembersParameters(cut).Get(p => p.SearchChanged).InvokeAsync("ann"));
        Assert.Equal("ann", MembersParameters(cut).Get(p => p.Search));

        cut.Render(p => p.Add(x => x.Team, "Sales & Ops"));
        Assert.Null(MembersParameters(cut).Get(p => p.Search));

        cut.Render(p => p.Add(x => x.Team, "Business"));
        Assert.Null(MembersParameters(cut).Get(p => p.Search));
    }

    /// <summary>The search text is reset when the Project parameter changes.</summary>
    [Fact]
    public async Task Search_IsReset_WhenTheProjectChanges()
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", "Marketing Project", "files");
        await cut.InvokeAsync(() => FilesParameters(cut).Get(p => p.SearchChanged).InvokeAsync("brief"));
        Assert.Equal("brief", FilesParameters(cut).Get(p => p.Search));

        cut.Render(p => p.Add(x => x.Project, "Other Project"));

        Assert.Equal("Other Project", FilesParameters(cut).Get(p => p.Project));
        Assert.Null(FilesParameters(cut).Get(p => p.Search));
    }

    /// <summary>A Team that appears in the catalog after the page loaded replaces the unknown-Team alert once the catalog raises <c>Changed</c>.</summary>
    [Fact]
    public async Task CatalogChanged_ATeamThatAppearsLater_ReplacesTheAlert()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Sales & Ops")] };
        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");
        Assert.Single(cut.FindAll(AlertSelector));
        Assert.Empty(RenderedTabs(cut));

        catalog.Teams = [Team("Sales & Ops"), Team("Business")];
        catalog.Raise();

        cut.WaitForAssertion(() => Assert.Equal([nameof(TeamMembers)], RenderedTabs(cut)));
        Assert.Empty(cut.FindAll(AlertSelector));
    }

    /// <summary>The page holds one catalog subscription while it lives and none after it is disposed.</summary>
    [Fact]
    public async Task Catalog_IsSubscribedWhileRendered_AndUnsubscribedOnDispose()
    {
        FakeTeamCatalog catalog = Catalog();
        await using MudBunitContext ctx = NewContext(catalog);
        _ = RenderPage(ctx, "Business");
        Assert.Equal(1, catalog.ChangedSubscriberCount);

        await ctx.DisposeComponentsAsync();

        Assert.Equal(0, catalog.ChangedSubscriberCount);
    }

    /// <summary>The test context: the fake catalog, both feature flags, and the three tab components replaced by parameter-capturing stubs.</summary>
    private static MudBunitContext NewContext(FakeTeamCatalog catalog, bool libraryEnabled = true, bool tasksEnabled = true)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        TeamOptions teamOptions = new();
        teamOptions.Library.Enabled = libraryEnabled;
        teamOptions.Tasks.Enabled = tasksEnabled;
        ctx.Services.AddSingleton(Options.Create(teamOptions));
        ctx.ComponentFactories.AddStub<TeamMembers>();
        ctx.ComponentFactories.AddStub<TeamFilesTab>();
        ctx.ComponentFactories.AddStub<TeamTasksTab>();
        return ctx;
    }

    /// <summary>Renders the page with the route values a router would bind.</summary>
    private static IRenderedComponent<TeamPage> RenderPage(MudBunitContext ctx, string team, string? project = null, string? tab = null) =>
        ctx.Render<TeamPage>(p => p
            .Add(x => x.Team, team)
            .Add(x => x.Project, project)
            .Add(x => x.Tab, tab));

    /// <summary>Business (with two Projects) and Sales &amp; Ops (with one), both with a member and a folder.</summary>
    private static FakeTeamCatalog Catalog() => new()
    {
        Teams = [Team("Business", ["Marketing Project", "Other Project"]), Team("Sales & Ops", ["Q4 #1"])],
    };

    /// <summary>A Team with a member and a folder unless told otherwise.</summary>
    private static TeamSummary Team(string name, IReadOnlyList<string>? projects = null, IReadOnlyList<string>? members = null, bool hasFolder = true) =>
        new(name, projects ?? [], members ?? ["Nova"], hasFolder);

    /// <summary>The trimmed text of every tab header, in order.</summary>
    private static string[] TabHeaders(IRenderedComponent<TeamPage> cut) =>
        [.. cut.FindAll(TabHeaderSelector).Select(header => header.TextContent.Trim())];

    /// <summary>The names of the tab components currently in the render tree.</summary>
    private static string[] RenderedTabs(IRenderedComponent<TeamPage> cut)
    {
        List<string> names = [];
        if (cut.FindComponents<Stub<TeamMembers>>().Count > 0)
        {
            names.Add(nameof(TeamMembers));
        }

        if (cut.FindComponents<Stub<TeamFilesTab>>().Count > 0)
        {
            names.Add(nameof(TeamFilesTab));
        }

        if (cut.FindComponents<Stub<TeamTasksTab>>().Count > 0)
        {
            names.Add(nameof(TeamTasksTab));
        }

        return [.. names];
    }

    /// <summary>The parameters the Members stub last received.</summary>
    private static CapturedParameterView<TeamMembers> MembersParameters(IRenderedComponent<TeamPage> cut) =>
        cut.FindComponent<Stub<TeamMembers>>().Instance.Parameters;

    /// <summary>The parameters the Files stub last received.</summary>
    private static CapturedParameterView<TeamFilesTab> FilesParameters(IRenderedComponent<TeamPage> cut) =>
        cut.FindComponent<Stub<TeamFilesTab>>().Instance.Parameters;

    /// <summary>The parameters the Tasks stub last received.</summary>
    private static CapturedParameterView<TeamTasksTab> TasksParameters(IRenderedComponent<TeamPage> cut) =>
        cut.FindComponent<Stub<TeamTasksTab>>().Instance.Parameters;

    /// <summary>The text with every run of whitespace collapsed to one space.</summary>
    private static string Words(string text) =>
        string.Join(' ', text.Split(WhitespaceChars, StringSplitOptions.RemoveEmptyEntries));
}
