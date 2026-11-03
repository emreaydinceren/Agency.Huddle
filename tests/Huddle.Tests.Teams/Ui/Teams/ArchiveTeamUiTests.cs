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
/// "Archive {Team}?" and whose body is the rest of T16. The sidebar's "Archived Teams" group is
/// <c>.teams-nav-archived</c>. The Team page lists <c>.team-archived-projects</c> and a Project page has
/// <c>button.team-project-archive</c>.
/// </summary>
public sealed class ArchiveTeamUiTests
{
    private const string T17 = "A Team named Home exists now. Rename one of them first.";

    /// <summary>The Details tab shows one Archive button.</summary>
    [Fact]
    public async Task DetailsTab_ShowsArchiveButton()
    {
        await using MudBunitContext ctx = NewDetailsContext(new FakeArchiveService());

        IRenderedComponent<ContainerFragment> cut = RenderDetails(ctx);

        Assert.Equal("Archive", Assert.Single(cut.FindAll("button.team-details-archive")).TextContent.Trim());
    }

    /// <summary>Clicking Archive opens the confirm: T16's question as the title, its counts and names as the body, then Cancel and Archive.</summary>
    [Fact]
    public async Task ArchiveClick_OpensConfirmWithT16()
    {
        FakeArchiveService archive = new() { Confirmation = new ArchiveConfirmation(3, 1, ["Ada", "Bo"]) };
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
        FakeArchiveService archive = new() { Confirmation = new ArchiveConfirmation(0, 0, []) };
        await using MudBunitContext ctx = NewDetailsContext(archive);
        IRenderedComponent<ContainerFragment> cut = RenderDetails(ctx);

        await ClickArchiveAsync(cut);

        Assert.Equal("0 open Tasks and 0 waiting on you will leave the Boards.", cut.Find(".mud-dialog-content").TextContent.Trim());
    }

    /// <summary>Cancel closes the confirm and the service archives nothing.</summary>
    [Fact]
    public async Task Cancel_ArchivesNothing()
    {
        FakeArchiveService archive = new();
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
        FakeArchiveService archive = new();
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
        await using MudBunitContext ctx = NewDetailsContext(new FakeArchiveService());
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
        FakeArchiveService archive = new() { ArchiveOutcome = new ArchiveOutcome(false, "Home has files open in another program. Close them and try again.") };
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

    /// <summary>An archived Team is a row under the sidebar's Archived Teams group with an Unarchive button; with none archived the group is absent.</summary>
    [Fact]
    public async Task Sidebar_ListsArchivedTeamsWithUnarchive()
    {
        FakeArchiveService archive = new() { ArchivedTeamNames = ["Old", "Older"] };
        await using MudBunitContext ctx = NewNavContext(archive);

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Equal("Archived Teams", cut.Find(".teams-nav-archived > .mud-nav-link .mud-nav-link-text").TextContent.Trim());
        Assert.Equal(["Old", "Older"], [.. cut.FindAll(".teams-nav-archived-row .teams-nav-archived-name").Select(name => name.TextContent.Trim())]);
        Assert.Equal(["Unarchive", "Unarchive"], [.. cut.FindAll(".teams-nav-archived-row button.teams-nav-archived-unarchive").Select(button => button.TextContent.Trim())]);
    }

    /// <summary>With no archived Team the Archived Teams group is not drawn.</summary>
    [Fact]
    public async Task Sidebar_NoArchivedTeams_DrawsNoGroup()
    {
        await using MudBunitContext ctx = NewNavContext(new FakeArchiveService());

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Empty(cut.FindAll(".teams-nav-archived"));
    }

    /// <summary>Unarchive calls the service once for that Team and the row goes when it succeeds.</summary>
    [Fact]
    public async Task Unarchive_Success_CallsOnceAndDropsTheRow()
    {
        FakeArchiveService archive = new() { ArchivedTeamNames = ["Old", "Older"] };
        await using MudBunitContext ctx = NewNavContext(archive);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => ArchivedRow(cut, "Older").QuerySelector("button.teams-nav-archived-unarchive")!.ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Older"], archive.Unarchived));
        cut.WaitForAssertion(() => Assert.Equal(["Old"], [.. cut.FindAll(".teams-nav-archived-row .teams-nav-archived-name").Select(name => name.TextContent.Trim())]));
    }

    /// <summary>A refused unarchive shows the service's reason (T17) as a warning snackbar and keeps the row.</summary>
    [Fact]
    public async Task Unarchive_Refused_ShowsT17()
    {
        FakeArchiveService archive = new() { ArchivedTeamNames = ["Home"], UnarchiveOutcome = new ArchiveOutcome(false, T17) };
        await using MudBunitContext ctx = NewNavContext(archive);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        await cut.InvokeAsync(() => ArchivedRow(cut, "Home").QuerySelector("button.teams-nav-archived-unarchive")!.ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(T17, Assert.Single(snackbar.ShownSnackbars).Message));
        Assert.Equal(Severity.Warning, snackbar.ShownSnackbars.Single().Severity);
        Assert.Single(cut.FindAll(".teams-nav-archived-row"));
    }

    /// <summary>The Team page lists the Team's archived Projects, each with an Unarchive that calls the service with the Team and Project.</summary>
    [Fact]
    public async Task TeamPage_ListsArchivedProjectsWithUnarchive()
    {
        FakeArchiveService archive = new() { ArchivedProjectNames = ["Alpha", "Beta"] };
        await using MudBunitContext ctx = NewPageContext(archive);
        IRenderedComponent<TeamPage> cut = RenderPage(ctx);

        Assert.Equal(["Alpha", "Beta"], [.. cut.FindAll(".team-archived-projects .team-archived-project-name").Select(name => name.TextContent.Trim())]);
        Assert.Equal("Archived Projects", cut.Find(".team-archived-projects-title").TextContent.Trim());
        await cut.InvokeAsync(() => cut.FindAll(".team-archived-project").Single(row => row.QuerySelector(".team-archived-project-name")!.TextContent.Trim() == "Beta")
            .QuerySelector("button.team-archived-project-unarchive")!.ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Business", "Beta")], archive.UnarchivedProjects));
    }

    /// <summary>A Team with no archived Project shows no list.</summary>
    [Fact]
    public async Task TeamPage_NoArchivedProjects_ShowsNoList()
    {
        await using MudBunitContext ctx = NewPageContext(new FakeArchiveService());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx);

        Assert.Empty(cut.FindAll(".team-archived-projects"));
    }

    /// <summary>The Project page's Archive button calls ArchiveProjectAsync with the Team and the Project, once, with no confirmation.</summary>
    [Fact]
    public async Task ProjectPage_Archive_CallsArchiveProjectAsync()
    {
        FakeArchiveService archive = new();
        await using MudBunitContext ctx = NewPageContext(archive);
        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Marketing");

        Assert.Equal("Archive", Assert.Single(cut.FindAll("button.team-project-archive")).TextContent.Trim());
        await cut.InvokeAsync(() => cut.Find("button.team-project-archive").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Business", "Marketing")], archive.ProjectsArchived));
    }

    /// <summary>A Team page (not a Project page) has no project-archive button.</summary>
    [Fact]
    public async Task TeamPage_HasNoProjectArchiveButton()
    {
        await using MudBunitContext ctx = NewPageContext(new FakeArchiveService());

        IRenderedComponent<TeamPage> cut = RenderPage(ctx);

        Assert.Empty(cut.FindAll("button.team-project-archive"));
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

    /// <summary>The sidebar row of the archived Team.</summary>
    private static AngleSharp.Dom.IElement ArchivedRow(IRenderedComponent<ContainerFragment> cut, string team) =>
        cut.FindAll(".teams-nav-archived-row").Single(row => string.Equals(row.QuerySelector(".teams-nav-archived-name")?.TextContent.Trim(), team, StringComparison.Ordinal));

    /// <summary>Renders the Details tab of Team Home with popovers and the dialog provider.</summary>
    private static IRenderedComponent<ContainerFragment> RenderDetails(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamDetails>(0);
            builder.AddAttribute(1, nameof(TeamDetails.Team), "Home");
            builder.CloseComponent();
        });

    /// <summary>Renders the sidebar Teams nav.</summary>
    private static IRenderedComponent<ContainerFragment> RenderNav(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.CloseComponent();
        });

    /// <summary>Renders the Business Team page, or its Project page when <paramref name="project"/> is given.</summary>
    private static IRenderedComponent<TeamPage> RenderPage(MudBunitContext ctx, string? project = null) =>
        ctx.Render<TeamPage>(p => p
            .Add(x => x.Team, "Business")
            .Add(x => x.Project, project));

    /// <summary>A context for the Details tab: a catalog holding Home, the build form stubbed, and the archive service.</summary>
    private static MudBunitContext NewDetailsContext(FakeArchiveService archive)
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
    private static MudBunitContext NewPageContext(FakeArchiveService archive)
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
    private static MudBunitContext NewNavContext(FakeArchiveService archive)
    {
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Live", [], ["Nova"], true)] };
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

    /// <summary>A recording <c>IArchiveService</c>: every call is kept, and the outcomes and lists are set by the test.</summary>
    private sealed class FakeArchiveService : IArchiveService
    {
        /// <summary>What <see cref="Confirm"/> returns.</summary>
        public ArchiveConfirmation Confirmation { get; init; } = new(0, 0, []);

        /// <summary>What <see cref="ArchiveTeamAsync"/> returns.</summary>
        public ArchiveOutcome ArchiveOutcome { get; init; } = new(true, null);

        /// <summary>What <see cref="UnarchiveTeamAsync"/> returns; a success removes the Team from <see cref="ArchivedTeamNames"/>.</summary>
        public ArchiveOutcome UnarchiveOutcome { get; init; } = new(true, null);

        /// <summary>The archived Teams the service's <c>ArchivedTeams</c> returns.</summary>
        public List<string> ArchivedTeamNames { get; init; } = [];

        /// <summary>The archived Projects the service's <c>ArchivedProjects</c> method returns for any Team.</summary>
        public List<string> ArchivedProjectNames { get; init; } = [];

        /// <summary>The Teams <see cref="Confirm"/> was asked about.</summary>
        public List<string> Confirmed { get; } = [];

        /// <summary>The Teams archived.</summary>
        public List<string> Archived { get; } = [];

        /// <summary>The Teams unarchived.</summary>
        public List<string> Unarchived { get; } = [];

        /// <summary>The (Team, Project) pairs archived.</summary>
        public List<(string Team, string Project)> ProjectsArchived { get; } = [];

        /// <summary>The (Team, Project) pairs unarchived.</summary>
        public List<(string Team, string Project)> UnarchivedProjects { get; } = [];

        /// <inheritdoc />
        public ArchiveConfirmation Confirm(string team)
        {
            this.Confirmed.Add(team);
            return this.Confirmation;
        }

        /// <inheritdoc />
        public Task<ArchiveOutcome> ArchiveTeamAsync(string team, CancellationToken ct)
        {
            this.Archived.Add(team);
            return Task.FromResult(this.ArchiveOutcome);
        }

        /// <inheritdoc />
        public Task<ArchiveOutcome> UnarchiveTeamAsync(string team, CancellationToken ct)
        {
            this.Unarchived.Add(team);
            if (this.UnarchiveOutcome.Done)
            {
                _ = this.ArchivedTeamNames.Remove(team);
            }

            return Task.FromResult(this.UnarchiveOutcome);
        }

        /// <inheritdoc />
        public Task<ArchiveOutcome> ArchiveProjectAsync(string team, string project, CancellationToken ct)
        {
            this.ProjectsArchived.Add((team, project));
            return Task.FromResult(new ArchiveOutcome(true, null));
        }

        /// <inheritdoc />
        public Task<ArchiveOutcome> UnarchiveProjectAsync(string team, string project, CancellationToken ct)
        {
            this.UnarchivedProjects.Add((team, project));
            return Task.FromResult(new ArchiveOutcome(true, null));
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ArchivedTeams() => [.. this.ArchivedTeamNames];

        /// <inheritdoc />
        public IReadOnlyList<string> ArchivedProjects(string team) => [.. this.ArchivedProjectNames];
    }
}
