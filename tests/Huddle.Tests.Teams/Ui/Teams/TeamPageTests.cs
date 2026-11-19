using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Forms;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tabs;
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

    private const string StatusSelector = ".team-page-notice[role='status']";

    /// <summary>The page declares exactly the four route templates of Spec §6.6, with <c>projects</c> a literal segment, plus the No project Library.</summary>
    [Fact]
    public void Routes_AreTheFourTemplatesOfTheSpec_AndTheNoProjectLibrary()
    {
        string[] templates = [.. typeof(TeamPage)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .OfType<RouteAttribute>()
            .Select(route => route.Template)
            .Order(StringComparer.Ordinal)];

        string[] expected =
        [
            "/teams/{Team}",
            "/teams/{Team}/no-project/files",
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

        Assert.Equal("Business", cut.FindComponent<Stub<TeamRoster>>().Instance.Parameters.Get(p => p.Team));
        Assert.Empty(cut.FindAll(AlertSelector));
    }

    /// <summary>Each feature-flag combination offers exactly the tabs of Spec §6.6, in order, and renders only the default tab's component.</summary>
    /// <param name="libraryEnabled"><c>Library:Enabled</c>.</param>
    /// <param name="tasksEnabled"><c>Tasks:Enabled</c>.</param>
    /// <param name="project">The Project, or <see langword="null"/> for a Team page.</param>
    /// <param name="expectedHeaders">The tab headers, comma-joined.</param>
    /// <param name="expectedStub">The one tab component that renders.</param>
    [Theory]
    [InlineData(true, true, null, "Members,Library,Manager Playbook,Agent Playbook,Details", nameof(TeamRoster))]
    [InlineData(true, true, "Marketing Project", "Library,Tasks", nameof(TeamFilesTab))]
    [InlineData(false, true, null, "Members,Manager Playbook,Agent Playbook,Details", nameof(TeamRoster))]
    [InlineData(false, true, "Marketing Project", "Tasks", nameof(TeamTasksTab))]
    [InlineData(true, false, null, "Members,Library,Manager Playbook,Agent Playbook,Details", nameof(TeamRoster))]
    [InlineData(true, false, "Marketing Project", "Library", nameof(TeamFilesTab))]
    [InlineData(false, false, null, "Members,Manager Playbook,Agent Playbook,Details", nameof(TeamRoster))]
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

        Assert.Equal("Files and Tasks are turned off in this installation.", cut.Find($"{StatusSelector} .team-page-notice-text").TextContent.Trim());
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
    [InlineData(null, "nonsense", true, nameof(TeamRoster))]
    [InlineData(null, "tasks", true, nameof(TeamRoster))]
    [InlineData("Marketing Project", "nonsense", true, nameof(TeamFilesTab))]
    [InlineData(null, "FILES", true, nameof(TeamFilesTab))]
    [InlineData(null, "files", false, nameof(TeamRoster))]
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

    /// <summary>
    /// The No project Library (<c>/teams/{Team}/no-project/files</c>) is headed "No project", draws no tabs, and shows the Team's
    /// folder with every Project folder hidden, so only the files that belong to no Project are left.
    /// </summary>
    [Fact]
    public async Task NoProjectRoute_ShowsTheTeamsFilesWithItsProjectFoldersHidden()
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/business/no-project/files");

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "business");

        Assert.Equal("No project", cut.Find("h1").TextContent.Trim());
        Assert.Empty(TabHeaders(cut));
        Assert.Equal([nameof(TeamFilesTab)], RenderedTabs(cut));
        Assert.Equal("Business", FilesParameters(cut).Get(p => p.Team));
        Assert.Equal(["Marketing Project", "Other Project"], FilesParameters(cut).Get(p => p.HiddenFolders));
    }

    /// <summary>A lower-case route spelling reaches the tabs and the Members stub as the catalog's own spelling.</summary>
    [Fact]
    public async Task Route_InAnotherCase_FeedsTheDisplaySpellingToTheTabs()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> members = RenderPage(ctx, "business");
        Assert.Equal("Business", members.FindComponent<Stub<TeamRoster>>().Instance.Parameters.Get(p => p.Team));

        IRenderedComponent<TeamPage> files = RenderPage(ctx, "business", "marketing PROJECT", "files");
        Assert.Equal("Business", FilesParameters(files).Get(p => p.Team));
        Assert.Equal("Marketing Project", FilesParameters(files).Get(p => p.Project));

        IRenderedComponent<TeamPage> tasks = RenderPage(ctx, "BUSINESS", "marketing project", "tasks");
        Assert.Equal("Business", TasksParameters(tasks).Get(p => p.Team));
        Assert.Equal("Marketing Project", TasksParameters(tasks).Get(p => p.Project));
    }

    /// <summary>The Team page and the Project page draw no breadcrumbs of their own: the shell's bar above every Tab page owns the trail.</summary>
    [Fact]
    public async Task Pages_DrawNoBreadcrumbsOfTheirOwn()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> team = RenderPage(ctx, "Business");
        IRenderedComponent<TeamPage> project = RenderPage(ctx, "Business", "Marketing Project");

        Assert.Empty(team.FindAll(".mud-breadcrumbs"));
        Assert.Empty(project.FindAll(".mud-breadcrumbs"));
    }

    /// <summary>Clicking a tab header navigates to the tab's route with escaped segments and never changes the page's own <c>Tab</c> parameter.</summary>
    /// <param name="team">The Team route value.</param>
    /// <param name="project">The Project route value, or <see langword="null"/>.</param>
    /// <param name="startTab">The <c>Tab</c> route value the page starts on, or <see langword="null"/>.</param>
    /// <param name="headerIndex">The header clicked.</param>
    /// <param name="expectedPath">The path navigated to.</param>
    [Theory]
    [InlineData("Business", null, null, 1, "/teams/Business/files")]
    [InlineData("Business", null, null, 2, "/teams/Business/manager-playbook")]
    [InlineData("Business", null, null, 3, "/teams/Business/agent-playbook")]
    [InlineData("Business", null, null, 4, "/teams/Business/details")]
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

        await cut.InvokeAsync(() => cut.FindAll(TabHeaderSelector)[headerIndex].ClickAsync());

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
        Assert.Equal([nameof(TeamRoster)], RenderedTabs(cut));

        cut.Render(p => p.Add(x => x.Tab, "files"));

        Assert.Equal([nameof(TeamFilesTab)], RenderedTabs(cut));
    }

    /// <summary>Disposing the page (a circuit teardown, or the Human leaving for another page) issues no navigation: only a Human's tab click does.</summary>
    /// <param name="tab">The tab the route shows when the page goes away.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("files")]
    [InlineData("process")]
    [InlineData("details")]
    public async Task Dispose_IssuesNoNavigation(string? tab)
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        ctx.Services.AddSingleton<ITeamFolders>(new FakeTeamFolders { ReadResult = NewTeamFile(Avatar.None) });
        ctx.ComponentFactories.AddStub<ProcessTab>();
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", tab: tab);
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal("Members,Library,Process,Manager Playbook,Agent Playbook,Details", string.Join(',', TabHeaders(cut)));
        Assert.Empty(navigation.History);

        await ctx.DisposeAsync();

        Assert.Equal([], navigation.History.Select(entry => entry.Uri).ToArray());
    }

    /// <summary>
    /// A teardown that disposes the tab panels before their <c>MudTabs</c> (the renderer disposes in the order of its component table,
    /// which a long-lived circuit has shuffled) moves MudTabs' active index once per panel; none of those moves is the Human's click, so
    /// the page issues no navigation. On a real lane this was a failed <c>NavigateTo</c> per panel in the server log, for a circuit nobody sees.
    /// </summary>
    /// <param name="tab">The tab the route shows when the circuit goes away.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("files")]
    [InlineData("process")]
    [InlineData("details")]
    public async Task TabPanelsDisposedBeforeTheirTabs_IssueNoNavigation(string? tab)
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        ctx.Services.AddSingleton<ITeamFolders>(new FakeTeamFolders { ReadResult = NewTeamFile(Avatar.None) });
        ctx.ComponentFactories.AddStub<ProcessTab>();
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", tab: tab);
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal("Members,Library,Process,Manager Playbook,Agent Playbook,Details", string.Join(',', TabHeaders(cut)));
        Assert.Empty(navigation.History);

        foreach (IRenderedComponent<MudTabPanel> panel in cut.FindComponents<MudTabPanel>())
        {
            await ctx.Renderer.Dispatcher.InvokeAsync(() => ((IAsyncDisposable)panel.Instance).DisposeAsync().AsTask());
        }

        Assert.Equal([], navigation.History.Select(entry => entry.Uri).ToArray());
    }

    /// <summary>The Human leaving the Team page (it is removed while the circuit lives) issues no navigation, so no tab bounces them back.</summary>
    /// <param name="tab">The tab the route shows when the page goes away.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("process")]
    [InlineData("details")]
    public async Task Removed_WhileTheCircuitLives_IssuesNoNavigation(string? tab)
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        ctx.Services.AddSingleton<ITeamFolders>(new FakeTeamFolders { ReadResult = NewTeamFile(Avatar.None) });
        ctx.ComponentFactories.AddStub<ProcessTab>();
        IRenderedComponent<PageSwitch> cut = ctx.Render<PageSwitch>(p => p.Add(x => x.Tab, tab));
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Single(cut.FindComponents<TeamPage>());

        cut.Render(p => p.Add(x => x.Show, false));

        Assert.Empty(cut.FindComponents<TeamPage>());
        Assert.Equal([], navigation.History.Select(entry => entry.Uri).ToArray());
    }

    /// <summary>Shows the Team page of Business while <see cref="Show"/> is set, so a test can remove it from a living renderer.</summary>
    private sealed class PageSwitch : ComponentBase
    {
        /// <summary>Whether the Team page is in the tree.</summary>
        [Parameter]
        public bool Show { get; set; } = true;

        /// <summary>The Team page's tab segment.</summary>
        [Parameter]
        public string? Tab { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            if (!this.Show)
            {
                return;
            }

            builder.OpenComponent<TeamPage>(0);
            builder.AddAttribute(1, nameof(TeamPage.Team), "Business");
            builder.AddAttribute(2, nameof(TeamPage.Tab), this.Tab);
            builder.CloseComponent();
        }
    }

    /// <summary>An unknown Team shows the Warning alert naming it with a link to the Agents page, and renders no tabs.</summary>
    /// <param name="project">A Project route value, or <see langword="null"/>: an unknown Team wins either way.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("Marketing Project")]
    public async Task UnknownTeam_ShowsTheAlertWithTheAgentsLink(string? project)
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Nope", project);

        Assert.Equal("There is no Team named \"Nope\".", cut.Find($"{AlertSelector} .team-page-notice-text").TextContent.Trim());
        Assert.Equal(Severity.Warning, cut.FindComponent<MudAlert>().Instance.Severity);
        AngleSharp.Dom.IElement link = cut.Find($"{AlertSelector} a.team-page-notice-link");
        Assert.Equal("/agents", link.GetAttribute("href"));
        Assert.Equal("Agents", link.TextContent.Trim());
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

        Assert.Equal("Business", cut.FindComponent<Stub<TeamRoster>>().Instance.Parameters.Get(p => p.Team));
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
        Assert.Equal("Members,Library,Manager Playbook,Agent Playbook,Details", string.Join(',', TabHeaders(cut)));
        Assert.Empty(cut.FindAll(AlertSelector));
    }

    /// <summary>Each tab keeps its own search text while the route switches tabs on the same Project, and the text is not carried across tabs.</summary>
    [Fact]
    public async Task Search_IsKeptPerTab_WhileSwitchingTabs()
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", "Marketing Project");

        cut.Render(p => p.Add(x => x.Tab, "files"));
        Assert.Null(FilesParameters(cut).Get(p => p.Search));
        await cut.InvokeAsync(() => FilesParameters(cut).Get(p => p.SearchChanged).InvokeAsync("brief"));
        Assert.Equal("brief", FilesParameters(cut).Get(p => p.Search));

        cut.Render(p => p.Add(x => x.Tab, "tasks"));
        Assert.Null(TasksParameters(cut).Get(p => p.Search));
        await cut.InvokeAsync(() => TasksParameters(cut).Get(p => p.SearchChanged).InvokeAsync("todo"));

        cut.Render(p => p.Add(x => x.Tab, (string?)null));
        cut.Render(p => p.Add(x => x.Tab, "files"));
        Assert.Equal("brief", FilesParameters(cut).Get(p => p.Search));
        cut.Render(p => p.Add(x => x.Tab, "tasks"));
        Assert.Equal("todo", TasksParameters(cut).Get(p => p.Search));
    }

    /// <summary>The search text is reset when the Team parameter changes, and not restored when it changes back.</summary>
    [Fact]
    public async Task Search_IsReset_WhenTheTeamChanges()
    {
        await using MudBunitContext ctx = NewContext(Catalog());
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", tab: "files");
        await cut.InvokeAsync(() => FilesParameters(cut).Get(p => p.SearchChanged).InvokeAsync("brief"));
        Assert.Equal("brief", FilesParameters(cut).Get(p => p.Search));

        cut.Render(p => p.Add(x => x.Team, "Sales & Ops"));
        Assert.Null(FilesParameters(cut).Get(p => p.Search));

        cut.Render(p => p.Add(x => x.Team, "Business"));
        Assert.Null(FilesParameters(cut).Get(p => p.Search));
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

        cut.WaitForAssertion(() => Assert.Equal([nameof(TeamRoster)], RenderedTabs(cut)));
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

        await ctx.DisposeComponentsOnDispatcherAsync();

        Assert.Equal(0, catalog.ChangedSubscriberCount);
    }

    /// <summary>The Team page names itself as its h1 through <see cref="PageHeader"/>, in the catalog's spelling, and the same text is the document title (DS-8).</summary>
    [Fact]
    public async Task TeamPage_Header_HasTheTeamNameAsItsH1()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "business");

        Assert.Equal("Business", Assert.Single(cut.FindAll("h1")).TextContent);
        Assert.Equal("Business", Assert.Single(cut.FindComponents<PageHeader>()).Instance.Title);
    }

    /// <summary>The Project page names itself as its h1 with the Project's name, and has no Duplicate button (UX-004).</summary>
    [Fact]
    public async Task ProjectPage_Header_HasTheProjectNameAsItsH1AndNoDuplicate()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", "marketing project");

        Assert.Equal("Marketing Project", Assert.Single(cut.FindAll("h1")).TextContent);
        Assert.Empty(cut.FindAll(".team-page-actions"));
        Assert.DoesNotContain(cut.FindAll("button"), button => string.Equals(button.TextContent.Trim(), "Duplicate", StringComparison.Ordinal));
    }

    /// <summary>Duplicate sits on the right of the Team page's header row, on the same line as the h1, at its natural width (UX-004).</summary>
    [Fact]
    public async Task TeamPage_Duplicate_SitsInTheHeaderRowBesideTheH1()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");

        IElement duplicate = Assert.Single(cut.FindAll("button.team-page-actions"), static button => button.TextContent.Trim() == "Duplicate");
        Assert.Same(cut.Find("h1").Closest(".page-header-row"), duplicate.Closest(".page-header-row"));
        Assert.DoesNotContain("team-page-duplicate", duplicate.ClassList);
    }

    /// <summary>The Details tab, last on a Team page, renders the Team's details with the catalog's spelling; the header has no Edit team button, and a Project page has no Details tab.</summary>
    [Fact]
    public async Task TeamPage_DetailsTab_RendersTheTeamsDetails()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> team = RenderPage(ctx, "business", tab: "details");
        IRenderedComponent<TeamPage> project = RenderPage(ctx, "Business", "marketing project");

        Assert.Equal([nameof(TeamDetails)], RenderedTabs(team));
        Assert.Equal("Business", team.FindComponent<Stub<TeamDetails>>().Instance.Parameters.Get(p => p.Team));
        Assert.Equal("Details", TabHeaders(team)[^1]);
        Assert.Empty(team.FindAll("a.team-page-edit"));
        Assert.DoesNotContain("Details", TabHeaders(project));
    }

    /// <summary>The Team page container is <c>MaxWidth.False</c> with <c>pa-4</c> padding, for a Team page and a Project page alike (UX-002, UX-003).</summary>
    /// <param name="project">The Project, or <see langword="null"/> for a Team page.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("Marketing Project")]
    public async Task Page_Container_IsWideWithSixteenPixelPadding(string? project)
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", project);

        Assert.Equal(MaxWidth.False, cut.FindComponent<MudContainer>().Instance.MaxWidth);
        Assert.Contains("pa-4", cut.Find(".mud-container").ClassList);
    }

    /// <summary>The tab rail takes its icons from <c>TabIcons</c>, the entity table of design-system.md (UX-008): Agents for Members, Library for Library, Tasks for a Project's Tasks; Details, which has no entity, takes the Info icon.</summary>
    [Fact]
    public async Task TabRail_IconsComeFromTabIcons()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");
        IRenderedComponent<TeamPage> project = RenderPage(ctx, "Business", "Marketing Project");

        string?[] icons = [.. cut.FindComponents<MudTabPanel>().Select(panel => panel.Instance.Icon)];
        Assert.Equal([TabIcons.For("agents"), TabIcons.For("library"), Icons.Material.Outlined.MenuBook, Icons.Material.Outlined.AutoStories, Icons.Material.Outlined.Info], icons);
        string?[] projectIcons = [.. project.FindComponents<MudTabPanel>().Select(panel => panel.Instance.Icon)];
        Assert.Equal([TabIcons.For("library"), TabIcons.For("tasks")], projectIcons);
    }

    /// <summary>The "turned off" notice is a status, not an alert: it says nothing went wrong (UX-V13 family, U19 carry-over).</summary>
    [Fact]
    public async Task FeaturesOffNotice_IsAStatus()
    {
        await using MudBunitContext ctx = NewContext(Catalog(), libraryEnabled: false, tasksEnabled: false);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business", "Marketing Project");

        Assert.Single(cut.FindAll(StatusSelector));
        Assert.Empty(cut.FindAll(AlertSelector));
    }

    /// <summary>The test context: the fake catalog, both feature flags, and the three tab components replaced by parameter-capturing stubs.</summary>
    private static MudBunitContext NewContext(FakeTeamCatalog catalog, bool libraryEnabled = true, bool tasksEnabled = true)
    {
        MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton<ITeamCatalog>(catalog))
            .WithTeamOptions(options =>
            {
                options.Library.Enabled = libraryEnabled;
                options.Tasks.Enabled = tasksEnabled;
            });
        ctx.ComponentFactories.AddStub<TeamRoster>();
        ctx.ComponentFactories.AddStub<TeamDetails>();
        ctx.ComponentFactories.AddStub<TeamFilesTab>();
        ctx.ComponentFactories.AddStub<TeamTasksTab>();
        ctx.ComponentFactories.AddStub<TeamSpend>();
        ctx.ComponentFactories.AddStub<ProjectSpend>();
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
        if (cut.FindComponents<Stub<TeamRoster>>().Count > 0)
        {
            names.Add(nameof(TeamRoster));
        }

        if (cut.FindComponents<Stub<TeamFilesTab>>().Count > 0)
        {
            names.Add(nameof(TeamFilesTab));
        }

        if (cut.FindComponents<Stub<TeamTasksTab>>().Count > 0)
        {
            names.Add(nameof(TeamTasksTab));
        }

        if (cut.FindComponents<Stub<TeamDetails>>().Count > 0)
        {
            names.Add(nameof(TeamDetails));
        }

        return [.. names];
    }

    /// <summary>The parameters the Files stub last received.</summary>
    private static CapturedParameterView<TeamFilesTab> FilesParameters(IRenderedComponent<TeamPage> cut) =>
        cut.FindComponent<Stub<TeamFilesTab>>().Instance.Parameters;

    /// <summary>The parameters the Tasks stub last received.</summary>
    private static CapturedParameterView<TeamTasksTab> TasksParameters(IRenderedComponent<TeamPage> cut) =>
        cut.FindComponent<Stub<TeamTasksTab>>().Instance.Parameters;

    /// <summary>
    /// The header avatar opens the Team's editor: rounded-square previews, no library card even though a library is configured, and a
    /// "Save" button. Saving a label writes it to the Team's <c>_team.md</c> through <c>UpdateTeamFile</c>, and cancelling writes nothing.
    /// </summary>
    [Fact]
    public async Task Avatar_Click_OpensTheLibraryFreeSquareEditor_AndSaveWritesTheTeamFile()
    {
        using TempDataDir dir = new();
        using TempDataDir library = new();
        FakeTeamFolders folders = new() { UpdateResult = true, ReadResult = NewTeamFile(Avatar.None) };
        await using MudBunitContext ctx = NewAvatarContext(dir, folders);
        _ = AvatarLibraryPickSupport.Register(ctx.Services, library.Path, null);
        IRenderedComponent<ContainerFragment> cut = RenderPageWithDialogs(ctx);

        await AgentProfileCardTestSupport.OpenAvatarEditorAsync(cut);

        Assert.Equal(["Initials of the name", "A short label", "Upload an image"], cut.FindAll(AgentProfileCardTestSupport.EditorDialog + " .avatar-choice-title").Select(title => title.TextContent.Trim()));
        int previews = cut.FindAll(AgentProfileCardTestSupport.EditorDialog + " .mud-avatar").Count;
        Assert.True(previews >= 4);
        Assert.Equal(previews, cut.FindAll(AgentProfileCardTestSupport.EditorDialog + " .mud-avatar.avatar-square").Count);

        await AgentProfileCardTestSupport.ClickEditorButtonAsync(cut, "Cancel");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(AgentProfileCardTestSupport.EditorDialog)));
        Assert.Equal(Avatar.None, folders.ReadResult?.Avatar);
        Assert.DoesNotContain("UpdateTeamFile:Business", folders.Calls);

        await AgentProfileCardTestSupport.OpenAvatarEditorAsync(cut);
        await AgentProfileCardTestSupport.SelectAvatarChoiceAsync(cut, "A short label");
        await AgentProfileCardTestSupport.SetAvatarLabelAsync(cut, "QA");
        await AgentProfileCardTestSupport.ClickEditorButtonAsync(cut, "Save");

        cut.WaitForAssertion(() => Assert.Equal(new Avatar("QA", null, null), folders.ReadResult?.Avatar));
        Assert.Contains("UpdateTeamFile:Business", folders.Calls);
    }

    /// <summary>The header avatar shows the Team's own avatar from the catalog, as a rounded square.</summary>
    [Fact]
    public async Task Avatar_Header_ShowsTheCatalogAvatarAsARoundedSquare()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business") with { TeamType = TeamFileType, Avatar = new Avatar("BZ", null, null) }] };
        await using MudBunitContext ctx = NewContext(catalog);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");

        IElement avatar = cut.Find("button.team-page-avatar .mud-avatar");
        Assert.Equal("BZ", avatar.TextContent.Trim());
        Assert.Contains("avatar-square", avatar.ClassList);
    }

    /// <summary>
    /// A Team with no <c>_team.md</c> (no team type) has nowhere to keep an avatar, so the header draws its rounded square
    /// without the button that would open an editor whose Save could only fail.
    /// </summary>
    [Fact]
    public async Task Avatar_TeamWithoutTeamFile_ShowsTheAvatarWithoutTheEditButton()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");

        Assert.Empty(cut.FindAll("button.team-page-avatar"));
        Assert.Contains("avatar-square", cut.Find(".page-header .mud-avatar").ClassList);
    }

    /// <summary>Saving an uploaded image writes it to the avatar folder, names it in <c>_team.md</c> and deletes the file it replaces.</summary>
    [Fact]
    public async Task Avatar_SaveUploadedImage_ReplacesAndDeletesThePreviousFile()
    {
        using TempDataDir dir = new();
        AvatarStore store = new(dir.Options(), NullLogger<AvatarStore>.Instance);
        string previous = store.WriteImage(AvatarLibraryPickSupport.MinimalPng, ".png");
        FakeTeamFolders folders = new() { UpdateResult = true, ReadResult = NewTeamFile(new Avatar(null, previous, null)) };
        await using MudBunitContext ctx = NewAvatarContext(dir, folders, store);
        IRenderedComponent<ContainerFragment> cut = RenderPageWithDialogs(ctx);

        await AgentProfileCardTestSupport.OpenAvatarEditorAsync(cut);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(AvatarLibraryPickSupport.MinimalPng, "team.png", contentType: "image/png"));
        await AgentProfileCardTestSupport.ClickEditorButtonAsync(cut, "Save");

        cut.WaitForAssertion(() => Assert.NotEqual(previous, folders.ReadResult?.Avatar.Image));
        string? written = folders.ReadResult?.Avatar.Image;
        Assert.NotNull(written);
        Assert.True(File.Exists(Path.Combine(store.ImageDirectory, written)));
        Assert.False(File.Exists(Path.Combine(store.ImageDirectory, previous)));
    }

    /// <summary>A Team with no readable team file cannot keep an avatar: the page tells the Human and removes the image it had just written.</summary>
    [Fact]
    public async Task Avatar_Save_WhenTheTeamFileCannotBeUpdated_RemovesTheNewImageAndWarns()
    {
        using TempDataDir dir = new();
        AvatarStore store = new(dir.Options(), NullLogger<AvatarStore>.Instance);
        FakeTeamFolders folders = new() { UpdateResult = false, ReadResult = null };
        await using MudBunitContext ctx = NewAvatarContext(dir, folders, store);
        IRenderedComponent<ContainerFragment> cut = RenderPageWithDialogs(ctx);

        await AgentProfileCardTestSupport.OpenAvatarEditorAsync(cut);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(AvatarLibraryPickSupport.MinimalPng, "team.png", contentType: "image/png"));
        await AgentProfileCardTestSupport.ClickEditorButtonAsync(cut, "Save");

        cut.WaitForAssertion(() => Assert.Contains("UpdateTeamFile:Business", folders.Calls));
        Assert.True(!Directory.Exists(store.ImageDirectory) || Directory.GetFiles(store.ImageDirectory).Length == 0);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        Assert.Equal("Business has no team file, so its avatar could not be saved.", Assert.Single(snackbar.ShownSnackbars).Message);
    }

    /// <summary>A bare team file for the avatar tests.</summary>
    private static TeamFile NewTeamFile(Avatar avatar) =>
        new(TeamFileType, null, null, null, string.Empty) { Avatar = avatar };

    /// <summary>The team type of a Team that has a <c>_team.md</c>; the page offers the avatar editor only to such a Team.</summary>
    private static readonly VersionedRef TeamFileType = new("development", "1");

    /// <summary>A context with the Business Team (which has a team file), a real avatar store over <paramref name="dir"/>, and <paramref name="folders"/>.</summary>
    private static MudBunitContext NewAvatarContext(TempDataDir dir, FakeTeamFolders folders, AvatarStore? store = null)
    {
        MudBunitContext ctx = NewContext(new FakeTeamCatalog { Teams = [Team("Business") with { TeamType = TeamFileType }] });
        ctx.Services.AddSingleton(store ?? new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddPersonaStore(dir);
        ctx.Services.AddSingleton<ITeamFolders>(folders);
        AvatarLibraryPickSupport.RegisterUnconfigured(ctx.Services);
        return ctx;
    }

    /// <summary>Renders the Business Team page beside the popover and dialog providers, so the avatar editor dialog paints.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPageWithDialogs(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamPage>(0);
            builder.AddAttribute(1, nameof(TeamPage.Team), "Business");
            builder.CloseComponent();
        });

    /// <summary>A Team with a team file has a spelled-out "Edit avatar…" button first in the header, then the shortcut toggle beside it, and pressing it opens the editor.</summary>
    [Fact]
    public async Task EditAvatarButton_SitsBeforeDuplicate_AndOpensTheEditor()
    {
        using TempDataDir dir = new();
        FakeTeamFolders folders = new() { UpdateResult = true, ReadResult = NewTeamFile(Avatar.None) };
        FakeTeamCatalog catalog = new() { Teams = [Team("Business") with { TeamType = new VersionedRef("development", "1") }] };
        await using MudBunitContext ctx = NewContext(catalog);
        ctx.Services.AddSingleton(new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddPersonaStore(dir);
        ctx.Services.AddSingleton<ITeamFolders>(folders);
        AvatarLibraryPickSupport.RegisterUnconfigured(ctx.Services);
        IRenderedComponent<ContainerFragment> cut = RenderPageWithDialogs(ctx);

        // The shortcut toggle draws once the stored shortcut choice is read, after the first render.
        cut.WaitForAssertion(() => Assert.Equal(
            ["Edit avatar…", "Hide shortcut", "Duplicate", "Archive"],
            cut.FindAll(".page-header-actions button").Select(static button => button.TextContent.Trim())));
        // Every header action is the same small outlined button, as Duplicate is.
        Assert.All(cut.FindAll(".page-header-actions button"), static button => Assert.Contains("mud-button-outlined", button.ClassList));
        await AgentProfileCardTestSupport.ClickAsync(cut, () => cut.Find(".page-header-actions button"));

        cut.WaitForElement(AgentProfileCardTestSupport.EditorDialog);
        Assert.True(AgentProfileCardTestSupport.HasEditorButton(cut, "Save"));
    }

    /// <summary>
    /// ADR-0083: the Project page leads its title with the Project's 24px sharp square avatar (its stored label), smaller than the Team
    /// page's 32px one; "Edit avatar…" sits before Archive and opens the editor with no picture cards, and Save writes the new label
    /// through <see cref="ITeamFolders.UpdateProjectAvatar"/>.
    /// </summary>
    [Fact]
    public async Task ProjectPage_Avatar_IsASmallSharpSquare_AndEditAvatarSavesTheLabel()
    {
        using TempDataDir dir = new();
        FakeTeamFolders folders = new() { UpdateResult = true };
        TeamSummary business = Team("Business", ["Launch"]) with
        {
            ProjectAvatars = new Dictionary<string, Avatar>(StringComparer.OrdinalIgnoreCase) { ["Launch"] = new("🚀", null, null) },
        };
        await using MudBunitContext ctx = NewContext(new FakeTeamCatalog { Teams = [business] });
        ctx.Services.AddSingleton(new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddPersonaStore(dir);
        ctx.Services.AddSingleton<ITeamFolders>(folders);
        AvatarLibraryPickSupport.RegisterUnconfigured(ctx.Services);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamPage>(0);
            builder.AddAttribute(1, nameof(TeamPage.Team), "Business");
            builder.AddAttribute(2, nameof(TeamPage.Project), "Launch");
            builder.CloseComponent();
        });

        IElement avatar = cut.Find("button.team-project-avatar .mud-avatar");
        Assert.Contains("avatar-sharp", avatar.ClassList);
        Assert.Contains("mud-avatar-small", avatar.ClassList);
        Assert.DoesNotContain("avatar-transcript", avatar.ClassList);
        Assert.Equal("🚀", avatar.TextContent.Trim());
        Assert.Equal(["Edit avatar…", "Needs You level…", "Archive"], cut.FindAll(".page-header-actions button").Select(static button => button.TextContent.Trim()));

        await AgentProfileCardTestSupport.ClickAsync(cut, () => cut.Find(".page-header-actions button"));
        cut.WaitForElement(AgentProfileCardTestSupport.EditorDialog);
        Assert.Equal(["Initials of the name", "A short label"], cut.FindAll(".avatar-choice-title").Select(static title => title.TextContent.Trim()));
        await AgentProfileCardTestSupport.SetAvatarLabelAsync(cut, "LA");
        await AgentProfileCardTestSupport.ClickEditorButtonAsync(cut, "Save");

        (string team, string project, Avatar saved) = Assert.Single(folders.ProjectAvatarWrites);
        Assert.Equal(("Business", "Launch", "LA"), (team, project, saved.Label));
    }

    /// <summary>A Team with no team file (no team type) has nowhere to keep an avatar, so it gets neither the avatar button nor the "Edit avatar…" button.</summary>
    [Fact]
    public async Task EditAvatarButton_TeamWithoutATeamFile_IsAbsent()
    {
        await using MudBunitContext ctx = NewContext(Catalog());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Business");

        cut.WaitForAssertion(() => Assert.Equal(
            ["Hide shortcut", "Duplicate", "Archive"],
            cut.FindAll(".page-header-actions button").Select(static button => button.TextContent.Trim())));
        Assert.Empty(cut.FindAll("button.team-page-avatar"));
    }
}
