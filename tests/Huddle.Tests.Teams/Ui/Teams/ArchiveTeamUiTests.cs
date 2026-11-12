using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins the archive UI (Spec 6.9, D-46, Settled texts T16 and T17). The UI talks to <c>IArchiveService</c>, a new
/// interface that <c>ArchiveService</c> implements (it is a sealed class with ten dependencies, so a fake needs the seam).
/// The Team's Details tab has <c>button.team-details-archive</c> ("Archive"); it opens the shared confirm whose title is
/// "Archive {Team}?" and whose body is the rest of T16. A Project page has <c>button.team-project-archive</c>, and the
/// sidebar's Team, header and Project rows have Archive menus. Restoring (and T17, its refusal) is the Archived page's,
/// pinned in <c>ArchivedPageTests</c>; the sidebar and the Team page no longer list what is archived.
/// </summary>
public sealed class ArchiveTeamUiTests
{
    /// <summary>The Details tab shows one Archive button.</summary>
    [Fact]
    public async Task DetailsTab_ShowsArchiveButton()
    {
        await using MudBunitContext ctx = NewDetailsContext(new RecordingArchiveService());

        IRenderedComponent<ContainerFragment> cut = RenderDetails(ctx);

        Assert.Equal("Archive", Assert.Single(cut.FindAll("button.team-details-archive")).TextContent.Trim());
    }

    /// <summary>Clicking Archive opens the confirm: T16's question as the title, its counts and names as the body, then Cancel and Archive.</summary>
    [Fact]
    public async Task ArchiveClick_OpensConfirmWithT16()
    {
        RecordingArchiveService archive = new() { Confirmation = new ArchiveConfirmation(3, 1, ["Ada", "Bo"]) };
        await using MudBunitContext ctx = NewDetailsContext(archive);
        IRenderedComponent<ContainerFragment> cut = RenderDetails(ctx);

        await ClickArchiveAsync(cut);

        Assert.Equal("Archive Home?", cut.Find(".mud-dialog-title").TextContent.Trim());
        // 3 open and 1 waiting come from the fake's confirmation; Ada and Bo are its NoTeamNames, joined with ", ".
        Assert.Equal("3 open Tasks and 1 waiting on you will leave the Boards. These Agents will have no Team: Ada, Bo.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(["Cancel", "Archive"], [.. cut.FindAll(".mud-dialog-actions button").Select(button => button.TextContent.Trim())]);
        Assert.Equal(["Home"], archive.Confirmed);
    }

    /// <summary>When no Agent is left without a Team the body is only the first sentence.</summary>
    [Fact]
    public async Task ArchiveClick_NoOrphanedAgents_OmitsTheSecondSentence()
    {
        RecordingArchiveService archive = new() { Confirmation = new ArchiveConfirmation(0, 0, []) };
        await using MudBunitContext ctx = NewDetailsContext(archive);
        IRenderedComponent<ContainerFragment> cut = RenderDetails(ctx);

        await ClickArchiveAsync(cut);

        Assert.Equal("0 open Tasks and 0 waiting on you will leave the Boards.", cut.Find(".mud-dialog-content").TextContent.Trim());
    }

    /// <summary>Cancel closes the confirm and the service archives nothing.</summary>
    [Fact]
    public async Task Cancel_ArchivesNothing()
    {
        RecordingArchiveService archive = new();
        await using MudBunitContext ctx = NewDetailsContext(archive);
        IRenderedComponent<ContainerFragment> cut = RenderDetails(ctx);
        await ClickArchiveAsync(cut);

        await cut.InvokeAsync(() => DialogButton(cut, "Cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Empty(archive.Archived);
    }

    /// <summary>Archive in the confirm archives the Team once.</summary>
    [Fact]
    public async Task ConfirmArchive_CallsTheServiceOnce()
    {
        RecordingArchiveService archive = new();
        await using MudBunitContext ctx = NewDetailsContext(archive);
        IRenderedComponent<ContainerFragment> cut = RenderDetails(ctx);
        await ClickArchiveAsync(cut);

        await cut.InvokeAsync(() => DialogButton(cut, "Archive").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Home"], archive.Archived));
    }

    /// <summary>After the Team is archived the page leaves its own (now archived) Team page for Home.</summary>
    [Fact]
    public async Task ConfirmArchive_Success_NavigatesAwayFromTheTeamPage()
    {
        await using MudBunitContext ctx = NewDetailsContext(new RecordingArchiveService());
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/teams/Home/details");
        IRenderedComponent<ContainerFragment> cut = RenderDetails(ctx);
        await ClickArchiveAsync(cut);

        await cut.InvokeAsync(() => DialogButton(cut, "Archive").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("http://localhost/", navigation.Uri));
    }

    /// <summary>A refused archive shows the reason as a warning snackbar and stays on the Team page.</summary>
    [Fact]
    public async Task ConfirmArchive_Refused_ShowsTheReasonAndStays()
    {
        RecordingArchiveService archive = new() { ArchiveOutcome = new ArchiveOutcome(false, "Home has files open in another program. Close them and try again.") };
        await using MudBunitContext ctx = NewDetailsContext(archive);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/teams/Home/details");
        IRenderedComponent<ContainerFragment> cut = RenderDetails(ctx);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        await ClickArchiveAsync(cut);

        await cut.InvokeAsync(() => DialogButton(cut, "Archive").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Home has files open in another program. Close them and try again.", Assert.Single(snackbar.ShownSnackbars).Message));
        Assert.Equal(Severity.Warning, snackbar.ShownSnackbars.Single().Severity);
        Assert.Equal("http://localhost/teams/Home/details", navigation.Uri);
    }

    /// <summary>The sidebar no longer lists archived Teams: with some archived, the Teams view draws no Archived Teams group or Unarchive (the Archived page restores them).</summary>
    [Fact]
    public async Task Sidebar_ArchivedTeams_AreNotListed()
    {
        RecordingArchiveService archive = new() { ArchivedTeamNames = ["Old", "Older"] };
        await using MudBunitContext ctx = NewNavContext(archive);

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx, teamsView: true);

        Assert.DoesNotContain(cut.FindAll(".mud-nav-link-text"), link => link.TextContent.Trim() == "Archived Teams");
        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Trim() == "Unarchive");
    }

    /// <summary>The Team page no longer lists its archived Projects: they are restored on the Archived page.</summary>
    [Fact]
    public async Task TeamPage_ArchivedProjects_AreNotListed()
    {
        await using MudBunitContext ctx = NewPageContext(new RecordingArchiveService { ArchivedProjectNames = ["Alpha", "Beta"] });

        IRenderedComponent<TeamPage> cut = RenderPage(ctx);

        Assert.DoesNotContain(cut.FindAll("*"), element => element.TextContent.Trim() == "Archived Projects");
        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Trim() == "Unarchive");
    }

    /// <summary>The Project page's Archive button calls ArchiveProjectAsync with the Team and the Project, once, with no confirmation.</summary>
    [Fact]
    public async Task ProjectPage_Archive_CallsArchiveProjectAsync()
    {
        RecordingArchiveService archive = new();
        await using MudBunitContext ctx = NewPageContext(archive);
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Marketing");

        Assert.Equal("Archive", Assert.Single(cut.FindAll("button.team-project-archive")).TextContent.Trim());
        await cut.InvokeAsync(() => cut.Find("button.team-project-archive").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Business", "Marketing")], archive.ProjectsArchived));
    }

    /// <summary>A Project page's title row carries one kind pill reading "Project" and a Team page's one reading "Team", each on the h1's line.</summary>
    [Fact]
    public async Task TitleRow_CarriesThePageKindPill()
    {
        await using MudBunitContext ctx = NewPageContext(new RecordingArchiveService());

        IRenderedComponent<TeamPage> project = RenderPage(ctx, "Marketing");
        Assert.Equal("Project", Assert.Single(project.FindAll(".page-header-title .page-kind-chip")).TextContent.Trim());

        IRenderedComponent<TeamPage> team = RenderPage(ctx);
        Assert.Equal("Team", Assert.Single(team.FindAll(".page-header-title .page-kind-chip")).TextContent.Trim());
    }

    /// <summary>The Team page's header Archive button asks T16's question for that Team and archives it once on Archive.</summary>
    [Fact]
    public async Task TeamPage_HeaderArchive_AsksThenArchivesTheTeam()
    {
        RecordingArchiveService archive = new();
        await using MudBunitContext ctx = NewPageContext(archive);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamPage>(0);
            builder.AddAttribute(1, nameof(TeamPage.Team), "Business");
            builder.CloseComponent();
        });

        Assert.Equal("Archive", Assert.Single(cut.FindAll(".page-header-actions button.team-page-archive")).TextContent.Trim());
        _ = cut.InvokeAsync(() => cut.Find("button.team-page-archive").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Archive Business?", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => DialogButton(cut, "Archive").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Business"], archive.Archived));
    }

    /// <summary>A Team page (not a Project page) has no project-archive button.</summary>
    [Fact]
    public async Task TeamPage_HasNoProjectArchiveButton()
    {
        await using MudBunitContext ctx = NewPageContext(new RecordingArchiveService());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx);

        Assert.Empty(cut.FindAll("button.team-project-archive"));
    }

    /// <summary>The Team row's menu in the list of every Team ends in Archive team…, which asks T16's question and archives the Team on Archive.</summary>
    [Fact]
    public async Task Sidebar_TeamRowMenu_ArchiveTeam_AsksThenArchives()
    {
        RecordingArchiveService archive = new();
        await using MudBunitContext ctx = NewNavContext(archive);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for Live']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-archive-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Archive Live?", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => DialogButton(cut, "Archive").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Live"], archive.Archived));
    }

    /// <summary>In the Teams view the Team's header row has its own "..." menu with Archive team…; Cancel in the confirm archives nothing.</summary>
    [Fact]
    public async Task Sidebar_TeamsView_HeaderMenu_ArchiveTeam_CancelArchivesNothing()
    {
        RecordingArchiveService archive = new();
        await using MudBunitContext ctx = NewNavContext(archive);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx, teamsView: true);
        // sidebar.css places the menu by its wrapper's class: a flex item in the header, over the trailing end on a Project row.
        Assert.Single(cut.FindAll(".teams-nav-scope-header .teams-nav-scope-menu"));

        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-header button[aria-label='Actions for Live']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-archive-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Archive Live?", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => DialogButton(cut, "Cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Equal(["Live"], archive.Confirmed);
        Assert.Empty(archive.Archived);
    }

    /// <summary>
    /// After a catalog change (another Team or Project archived meanwhile) the header menu still closes before T16's confirm opens:
    /// a menu left open would sit above the dialog's scrim. Blazor sets a menu's ref only when it creates the menu, so clearing the
    /// refs on a catalog change once left this one without its ref.
    /// </summary>
    [Fact]
    public async Task Sidebar_TeamsView_HeaderMenu_AfterACatalogChange_ClosesBeforeTheConfirm()
    {
        RecordingArchiveService archive = new();
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Live", [], ["Nova"], true)] };
        await using MudBunitContext ctx = NewNavContext(archive, catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx, teamsView: true);
        await cut.InvokeAsync(catalog.Raise);

        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-header button[aria-label='Actions for Live']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-archive-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Archive Live?", cut.Find(".mud-dialog-title").TextContent.Trim()));

        Assert.Empty(cut.FindAll(".mud-popover-open .teams-nav-archive-team"));
    }

    /// <summary>
    /// The Team row's menu in the list of every Team also closes before T16's confirm after a catalog change: its ref is set
    /// only when the menu is created, so clearing the refs on a catalog change once left it open above the dialog's scrim.
    /// </summary>
    [Fact]
    public async Task Sidebar_TeamRowMenu_AfterACatalogChange_ClosesBeforeTheConfirm()
    {
        RecordingArchiveService archive = new();
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Live", [], ["Nova"], true)] };
        await using MudBunitContext ctx = NewNavContext(archive, catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        await cut.InvokeAsync(catalog.Raise);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for Live']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-archive-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Archive Live?", cut.Find(".mud-dialog-title").TextContent.Trim()));

        Assert.Empty(cut.FindAll(".mud-popover-open .teams-nav-archive-team"));
    }

    /// <summary>
    /// Archiving the last Team from the sidebar closes its Tabs (its page and its Project's) at once and, with no Tab left,
    /// goes Home. TabSync would leave them open, because it reads an empty catalog as "not loaded yet".
    /// </summary>
    [Fact]
    public async Task Sidebar_ArchiveTheLastTeam_ClosesItsTabs_AndGoesHome()
    {
        RecordingArchiveService archive = new();
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Live", ["Alpha"], ["Nova"], true)] };
        await using MudBunitContext ctx = NewNavContext(archive, catalog);
        Agency.Huddle.App.Tabs.TabService tabs = ctx.Services.GetRequiredService<Agency.Huddle.App.Tabs.TabService>();
        tabs.Keep(tabs.Open("/teams/Live/projects/Alpha").Id);
        tabs.Keep(tabs.Open("/teams/Live").Id);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/teams/Live");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for Live']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-archive-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => DialogButton(cut, "Archive").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(tabs.Tabs));
        Assert.Equal("http://localhost/", navigation.Uri);
    }

    /// <summary>A Team shortcut's view raises TeamGone once the catalog no longer lists its Team, so the layout can stop showing an empty view; the Teams view never does.</summary>
    [Fact]
    public async Task Sidebar_ShortcutTeamLeavesTheCatalog_RaisesTeamGone()
    {
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Live", [], ["Nova"], true), new TeamSummary("Other", [], ["Nova"], true)] };
        await using MudBunitContext ctx = NewNavContext(new RecordingArchiveService(), catalog);
        List<string> gone = [];
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.AddAttribute(1, nameof(TeamsNav.Team), "Live");
            builder.AddAttribute(2, nameof(TeamsNav.TeamGone), EventCallback.Factory.Create<string>(new object(), gone.Add));
            builder.CloseComponent();
            builder.OpenComponent<TeamsNav>(3);
            builder.AddAttribute(4, nameof(TeamsNav.Team), "Live");
            builder.AddAttribute(5, nameof(TeamsNav.IsTeamsView), true);
            builder.AddAttribute(6, nameof(TeamsNav.TeamGone), EventCallback.Factory.Create<string>(new object(), gone.Add));
            builder.CloseComponent();
        });

        catalog.Teams = [new TeamSummary("Other", [], ["Nova"], true)];
        await cut.InvokeAsync(catalog.Raise);

        cut.WaitForAssertion(() => Assert.Equal(["Live"], gone));
    }

    /// <summary>A Project row's "..." menu holds Archive project, which archives that Project of its Team at once, with no confirmation.</summary>
    [Fact]
    public async Task Sidebar_ProjectRowMenu_ArchiveProject_ArchivesAtOnce()
    {
        RecordingArchiveService archive = new();
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Live", ["Marketing", "Other"], ["Nova"], true)] };
        await using MudBunitContext ctx = NewNavContext(archive, catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx, teamsView: true);

        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-row[data-scope='project'] button[aria-label='Actions for Marketing']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-archive-project").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Live", "Marketing")], archive.ProjectsArchived));
        Assert.Empty(cut.FindAll(".mud-dialog-title"));
    }

    /// <summary>A refused Project archive shows the reason as a warning snackbar.</summary>
    [Fact]
    public async Task Sidebar_ProjectRowMenu_Refused_ShowsTheReason()
    {
        const string Reason = "Marketing has files open in another program. Close them and try again.";
        RecordingArchiveService archive = new() { ArchiveProjectOutcome = new ArchiveOutcome(false, Reason) };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Live", ["Marketing"], ["Nova"], true)] };
        await using MudBunitContext ctx = NewNavContext(archive, catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx, teamsView: true);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for Marketing']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-archive-project").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(Reason, Assert.Single(snackbar.ShownSnackbars).Message));
        Assert.Equal(Severity.Warning, snackbar.ShownSnackbars.Single().Severity);
    }

    /// <summary>Starts the Archive click without awaiting it (the handler awaits the dialog) and waits for the confirm.</summary>
    private static async Task ClickArchiveAsync(IRenderedComponent<ContainerFragment> cut)
    {
        _ = cut.InvokeAsync(() => cut.Find("button.team-details-archive").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
    }

    /// <summary>The confirm's button with the given text.</summary>
    private static AngleSharp.Dom.IElement DialogButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll(".mud-dialog-actions button").Single(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>Renders the Details tab of Team Home with popovers and the dialog provider.</summary>
    private static IRenderedComponent<ContainerFragment> RenderDetails(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamDetails>(0);
            builder.AddAttribute(1, nameof(TeamDetails.Team), "Home");
            builder.CloseComponent();
        });

    /// <summary>Renders the sidebar Teams nav.</summary>
    private static IRenderedComponent<ContainerFragment> RenderNav(MudBunitContext ctx, bool teamsView = false) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.AddAttribute(1, nameof(TeamsNav.IsTeamsView), teamsView);
            builder.CloseComponent();
        });

    /// <summary>Renders the Business Team page, or its Project page when <paramref name="project"/> is given.</summary>
    private static IRenderedComponent<TeamPage> RenderPage(MudBunitContext ctx, string? project = null) =>
        ctx.Render<TeamPage>(p => p
            .Add(x => x.Team, "Business")
            .Add(x => x.Project, project));

    /// <summary>A context for the Details tab: a catalog holding Home, the build form stubbed, and the archive service.</summary>
    private static MudBunitContext NewDetailsContext(RecordingArchiveService archive)
    {
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Home", [], ["Nova"], true)] };
        MudBunitContext ctx = UiTestHost.Create(services =>
        {
            services.AddSingleton<ITeamCatalog>(catalog);
            services.AddSingleton<IArchiveService>(archive);
        });
        ctx.ComponentFactories.AddStub<BuildTeam>();
        return ctx;
    }

    /// <summary>A context for the Team page: Business with two Projects, the tab components stubbed, and the archive service.</summary>
    private static MudBunitContext NewPageContext(RecordingArchiveService archive)
    {
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Business", ["Marketing", "Other"], ["Nova"], true)] };
        MudBunitContext ctx = UiTestHost.Create(services =>
        {
            services.AddSingleton<ITeamCatalog>(catalog);
            services.AddSingleton<IArchiveService>(archive);
        }).WithTeamOptions(options =>
        {
            options.Library.Enabled = true;
            options.Tasks.Enabled = true;
        });
        ctx.ComponentFactories.AddStub<TeamRoster>();
        ctx.ComponentFactories.AddStub<TeamDetails>();
        ctx.ComponentFactories.AddStub<TeamFilesTab>();
        ctx.ComponentFactories.AddStub<TeamTasksTab>();
        ctx.ComponentFactories.AddStub<TeamSpend>();
        return ctx;
    }

    /// <summary>A context for the sidebar Teams nav (the services TeamsNavTests registers) plus the archive service; the catalog holds one live Team.</summary>
    private static MudBunitContext NewNavContext(RecordingArchiveService archive, FakeTeamCatalog? teams = null)
    {
        FakeTeamCatalog catalog = teams ?? new() { Teams = [new TeamSummary("Live", [], ["Nova"], true)] };
        MudBunitContext ctx = new();
        ITeamDirectory rooms = ScriptedRoomsDirectory.Create(out _);
        ctx.Services.AddSingleton(rooms);
        ctx.Services.AddSingleton<TempDataDir>();
        ctx.Services.AddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));
        ctx.Services.AddSingleton(sp => new Agency.Huddle.App.NeedsYou.NeedsYouTracker(
            sp.GetRequiredService<RoomEvents>(),
            new QuestionStore(sp.GetRequiredService<RoomEvents>()),
            new Agency.Huddle.App.Elicitation.ElicitationStore(sp.GetRequiredService<RoomEvents>()),
            ScriptedRoomsDirectory.Create(out _),
            NullLogger<Agency.Huddle.App.NeedsYou.NeedsYouTracker>.Instance));
        ctx.Services.AddSingleton<ChatService>(sp =>
        {
            TempDataDir dir = sp.GetRequiredService<TempDataDir>();
            RoomEvents events = sp.GetRequiredService<RoomEvents>();
            FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
            return new ChatService(rooms, store, events, new FakeMentionAliasSource(), TestOptions.Create(), new ProposalStore(events), new QuestionStore(events), NullLogger<ChatService>.Instance) { TeamCatalog = catalog };
        });
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(sp => new AvatarStore(sp.GetRequiredService<TempDataDir>().Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddPersonaStore();
        ctx.Services.AddTaskStore();
        ctx.Services.AddTeamTypes();
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton<ITeamFolders>(new FakeTeamFolders());
        ctx.Services.AddSingleton(sp => new Agency.Huddle.App.Tasks.Views.ViewStore(sp.GetRequiredService<TempDataDir>().Options(), NullLogger<Agency.Huddle.App.Tasks.Views.ViewStore>.Instance));
        ctx.Services.AddSingleton(Options.Create(new TeamOptions()));
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        ctx.Services.AddSingleton<IArchiveService>(archive);
        ctx.JSInterop.Setup<string?>("huddleStorage.get", _ => true).SetResult(null);
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        return ctx;
    }
}
