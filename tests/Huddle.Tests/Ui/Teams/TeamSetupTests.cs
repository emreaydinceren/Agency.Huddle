using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Teammates;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Task 31 (HR-1, HR-2, TT-2) for the Create Team screen <c>TeamSetup</c>: the two routes, the
/// type dropdown in catalog order, the workspace field shown only for a type that has one and required
/// when the type says so, and Create making the Team folder, its <c>_team.md</c> and its manager. A
/// manager card that needs setup still creates the Team and is shown again on the setup route.
/// </summary>
public sealed class TeamSetupTests
{
    private const string TitleInput = "div.team-setup-title input";

    private const string WorkspaceInput = "div.team-setup-workspace input";

    private const string CreateButton = "button.team-setup-create";

    private const string Problem = ".team-setup-problem";

    private const string AlphaManifest = """
        { "schemaVersion": 1, "id": "alpha", "version": "1.0.0", "displayName": "Alpha", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true, "suggestedNames": ["Ada", "Grace"] },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 2 }
          ] }
        """;

    private const string BetaManifest = """
        { "schemaVersion": 1, "id": "beta", "version": "2.0.0", "displayName": "Beta", "order": 2,
          "workspace": { "kind": "folder", "label": "Project folder", "required": true },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true, "suggestedNames": ["Ada", "Grace"] }
          ] }
        """;

    private const string GammaManifest = """
        { "schemaVersion": 1, "id": "gamma", "version": "1.0.0", "displayName": "Gamma", "order": 3,
          "workspace": { "kind": "repo", "label": "Repository", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true, "suggestedNames": ["Ada", "Grace"] }
          ] }
        """;

    private static readonly string[] TypeIds = ["alpha", "beta", "gamma"];

    /// <summary>The page declares exactly the two routes of Task 31.</summary>
    [Fact]
    public void Routes_AreNewAndSetup()
    {
        string[] templates = [.. typeof(TeamSetup)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .OfType<RouteAttribute>()
            .Select(route => route.Template)
            .Order(StringComparer.Ordinal)];

        Assert.Equal(["/teams/new", "/teams/{Team}/setup"], templates);
    }

    /// <summary>The type dropdown lists the catalog's types in catalog order (TT-2), by display name.</summary>
    [Fact]
    public async Task TypeDropdown_ListsTypesInCatalogOrder()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: null);

        IReadOnlyList<IElement> options = await TeammateCardTestSupport.OpenSelectAndListOptionsAsync(cut, "Team type");

        Assert.Equal(["Alpha", "Beta", "Gamma"], options.Select(option => option.TextContent.Trim()));
    }

    /// <summary>The workspace field is hidden for a type whose workspace is None and shown, labelled by the type, for the others.</summary>
    [Fact]
    public async Task WorkspaceField_HiddenForNone_ShownOtherwise()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: null);

        Assert.Empty(cut.FindAll(WorkspaceInput));

        await ChooseTypeAsync(cut, "Beta");

        Assert.Single(cut.FindAll(WorkspaceInput));
        Assert.Equal("Project folder", cut.Find("div.team-setup-workspace label").TextContent.Trim());

        await ChooseTypeAsync(cut, "Alpha");

        Assert.Empty(cut.FindAll(WorkspaceInput));
    }

    /// <summary>Create makes the Team folder and its <c>_team.md</c>, hires the manager, records the manager in the file and goes to the setup route.</summary>
    [Fact]
    public async Task Create_MakesFolderTeamFileAndManager()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: null);

        await TypeAsync(cut, TitleInput, "Alpha Team");
        await ClickCreateAsync(cut);

        string teamFile = Assert.Single(Directory.EnumerateFiles(fixture.Dir.Path, "_team.md", SearchOption.AllDirectories));
        Assert.Equal("Alpha Team", new DirectoryInfo(Path.GetDirectoryName(teamFile) ?? string.Empty).Name);
        Assert.True(TeamFile.TryParse(File.ReadAllText(teamFile), out TeamFile? file));
        Assert.Equal("alpha", file.TeamType.Id);
        Assert.Equal("1.0.0", file.TeamType.Version);
        Assert.NotNull(file.Manager);
        Persona? manager = fixture.Personas.Get(file.Manager);
        Assert.NotNull(manager);
        Assert.True(PersonaFrontmatter.TryReadIdentity(manager.Text, out PersonaIdentity? identity, out _));
        Assert.Equal(["Alpha Team"], identity.Teams);
        Assert.Equal("alpha/lead@1.0.0", identity.Role?.ToString());
        Assert.Equal("teams/Alpha%20Team/setup", ctx.Services.GetRequiredService<NavigationManager>().ToBaseRelativePath(ctx.Services.GetRequiredService<NavigationManager>().Uri));
    }

    /// <summary>A title that matches an existing Team shows the exact refusal and Create stays disabled; nothing is created.</summary>
    [Fact]
    public async Task Create_ExistingTeamName_ShowsErrorAndCreatesNothing()
    {
        using Fixture fixture = new();
        fixture.Catalog.Teams = [new TeamSummary("Alpha Team", [], [], true)];
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: null);

        await TypeAsync(cut, TitleInput, "alpha team");

        Assert.Equal("A Team with that name exists.", cut.Find(Problem).TextContent.Trim());
        Assert.True(cut.Find(CreateButton).HasAttribute("disabled"));
        Assert.Empty(Directory.EnumerateFiles(fixture.Dir.Path, "_team.md", SearchOption.AllDirectories));
    }

    /// <summary>A required workspace that is blank or not an existing folder blocks Create with its reason; an existing folder enables it.</summary>
    [Fact]
    public async Task Create_InvalidWorkspace_BlocksCreate()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: null);
        await ChooseTypeAsync(cut, "Beta");
        await TypeAsync(cut, TitleInput, "Beta Team");

        Assert.True(cut.Find(CreateButton).HasAttribute("disabled"));
        Assert.Equal("Choose a workspace folder.", cut.Find(Problem).TextContent.Trim());

        await TypeAsync(cut, WorkspaceInput, Path.Combine(fixture.Dir.Path, "nowhere"));

        Assert.True(cut.Find(CreateButton).HasAttribute("disabled"));
        Assert.Equal("That folder does not exist.", cut.Find(Problem).TextContent.Trim());

        await TypeAsync(cut, WorkspaceInput, fixture.Dir.Path);

        Assert.False(cut.Find(CreateButton).HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(Problem));
    }

    /// <summary>A manager card that needs setup still creates the Team without hiring; the setup route then shows the card's reasons.</summary>
    [Fact]
    public async Task Create_ManagerNeedsSetup_CreatesTeamWithoutManagerAndShowsReasons()
    {
        using Fixture fixture = new(new NoAdapterContext());
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: null);

        await TypeAsync(cut, TitleInput, "Alpha Team");
        await ClickCreateAsync(cut);

        string teamFile = Assert.Single(Directory.EnumerateFiles(fixture.Dir.Path, "_team.md", SearchOption.AllDirectories));
        Assert.True(TeamFile.TryParse(File.ReadAllText(teamFile), out TeamFile? file));
        Assert.Null(file.Manager);
        Assert.Empty(fixture.Personas.ListNames());
        Assert.Equal("teams/Alpha%20Team/setup", ctx.Services.GetRequiredService<NavigationManager>().ToBaseRelativePath(ctx.Services.GetRequiredService<NavigationManager>().Uri));

        await using MudBunitContext setupCtx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> setup = Render(setupCtx, team: "Alpha Team");

        TeamTypeManifest type = Assert.IsType<TeamTypeManifest>(fixture.Types.Find("alpha"));
        CardState expected = CardResolver.Resolve(type.Roles[0], [], [], new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal), [], [], new EmptyConnectorStatus());
        Assert.Equal(CardReadiness.NeedsSetup, expected.Readiness);
        Assert.Equal(expected.Reasons, setup.FindAll(".team-setup-reason").Select(reason => reason.TextContent.Trim()));
        Assert.True(setup.Find("button.team-setup-hire").HasAttribute("disabled"));
    }

    /// <summary>On the setup route a Ready manager card is hired by the button and recorded in the Team file.</summary>
    [Fact]
    public async Task Setup_ReadyManager_HireButtonHiresAndRecordsManager()
    {
        using Fixture fixture = new();
        TeamFile created = new(new VersionedRef("alpha", "1.0.0"), null, null, null, "Process.md", string.Empty);
        Assert.Equal(TeamCreateOutcome.Created, fixture.Folders.CreateTeam("Alpha Team", created).Outcome);
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: "Alpha Team");

        await cut.InvokeAsync(() => cut.Find("button.team-setup-hire").ClickAsync());

        TeamFile? file = fixture.Folders.ReadTeamFile("Alpha Team");
        Assert.NotNull(file?.Manager);
        Assert.NotNull(fixture.Personas.Get(file.Manager));
        Assert.Empty(cut.FindAll("button.team-setup-hire"));
    }

    /// <summary>The folder service's refusals are shown verbatim as the page's own messages and the page stays on the form.</summary>
    [Fact]
    public async Task Create_FolderServiceRefuses_ShowsMessage()
    {
        using Fixture fixture = new();
        FakeTeamFolders folders = new() { CreateTeamResult = new TeamCreateResult(TeamCreateOutcome.AlreadyExists, null) };
        await using MudBunitContext ctx = fixture.NewContext(folders);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: null);
        await TypeAsync(cut, TitleInput, "Alpha Team");

        await ClickCreateAsync(cut);

        Assert.Equal("A Team with that name exists.", cut.Find(Problem).TextContent.Trim());

        folders.CreateTeamResult = new TeamCreateResult(TeamCreateOutcome.InvalidName, null);
        await ClickCreateAsync(cut);

        Assert.Equal("That Team name is not allowed.", cut.Find(Problem).TextContent.Trim());
        Assert.DoesNotContain("/setup", ctx.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    /// <summary>The setup route for a Team with no readable <c>_team.md</c> says so and offers nothing to hire.</summary>
    [Fact]
    public async Task Setup_UnknownTeam_ShowsNotice()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: "Ghost");

        Assert.Equal("This Team has no setup file.", cut.Find(".team-setup-notice").TextContent.Trim());
        Assert.Empty(cut.FindAll("button.team-setup-hire"));
    }

    /// <summary>The create screen opens with <c>PageHeader</c> (UX-001): the page's only h1 at Typo.h5, "New team".</summary>
    [Fact]
    public async Task NewMode_Header_IsNewTeamAsTheOnlyH1()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: null);

        IElement heading = Assert.Single(cut.FindAll("h1"));
        Assert.Equal("New team", heading.TextContent.Trim());
        Assert.Contains("mud-typography-h5", heading.ClassList);
        Assert.NotNull(cut.FindComponent<PageHeader>());
    }

    /// <summary>The setup route opens with <c>PageHeader</c> too: the h1 reads "Set up" and the Team name, and the old h5 heading is gone (UX-001).</summary>
    [Fact]
    public async Task SetupMode_Header_IsSetUpTeamAsTheOnlyH1()
    {
        using Fixture fixture = new();
        TeamFile created = new(new VersionedRef("alpha", "1.0.0"), null, null, null, "Process.md", string.Empty);
        Assert.Equal(TeamCreateOutcome.Created, fixture.Folders.CreateTeam("Alpha Team", created).Outcome);
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx, team: "Alpha Team");

        Assert.Equal("Set up Alpha Team", Assert.Single(cut.FindAll("h1")).TextContent.Trim());
        Assert.Empty(cut.FindAll(".team-setup-heading"));
    }

    /// <summary>The container carries <c>pa-4</c> on every route: the old <c>py-4</c> left no side gutter (UX-001, UX-003).</summary>
    /// <param name="team">The Team of the setup route, or <see langword="null"/> for the create screen.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("Alpha Team")]
    public async Task Container_HasSixteenPixelPaddingOnAllSides(string? team)
    {
        using Fixture fixture = new();
        TeamFile created = new(new VersionedRef("alpha", "1.0.0"), null, null, null, "Process.md", string.Empty);
        Assert.Equal(TeamCreateOutcome.Created, fixture.Folders.CreateTeam("Alpha Team", created).Outcome);
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx, team);

        IElement container = cut.Find(".team-setup.mud-container");
        Assert.Contains("pa-4", container.ClassList);
        Assert.DoesNotContain("py-4", container.ClassList);
    }

    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, string? team)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamSetup>(0);
            if (team is not null)
            {
                builder.AddAttribute(1, nameof(TeamSetup.Team), team);
            }

            builder.CloseComponent();
        });
    }

    private static async Task ChooseTypeAsync(IRenderedComponent<ContainerFragment> cut, string displayName)
    {
        IReadOnlyList<IElement> options = await TeammateCardTestSupport.OpenSelectAndListOptionsAsync(cut, "Team type");
        IElement option = options.First(item => string.Equals(item.TextContent.Trim(), displayName, StringComparison.Ordinal));
        await cut.InvokeAsync(() => option.ClickAsync());
    }

    private static async Task TypeAsync(IRenderedComponent<ContainerFragment> cut, string selector, string text)
    {
        await cut.InvokeAsync(() => cut.Find(selector).InputAsync(text));
    }

    private static async Task ClickCreateAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await cut.InvokeAsync(() => cut.Find(CreateButton).ClickAsync());
    }

    /// <summary>A hire context in which no adapter answered, so every card needs setup.</summary>
    private sealed class NoAdapterContext : IHireContext
    {
        public IReadOnlyList<AdapterAvailability> Answered { get; } = [];

        public IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> TierMap { get; } =
            new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> AvoidAdapters { get; } = [];

        public IConnectorStatus Connectors { get; } = new EmptyConnectorStatus();
    }

    /// <summary>The real stores behind the page: three team types, a Persona store, the real folder provisioner and a hire service.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly LibraryFileServiceFixture library;
        private readonly AvatarStore avatars;
        private readonly IHireContext hireContext;

        public Fixture(IHireContext? hireContext = null)
        {
            this.hireContext = hireContext ?? new RoleFixtures.Context();
            this.Dir = new TempDataDir();
            this.library = LibraryFileServiceFixture.Attach(this.Dir.Path);
            this.Personas = TestTaskStore.CreatePersonaStore(this.Dir);
            this.Catalog = new FakeTeamCatalog();
            Dictionary<string, string> builtins = new(StringComparer.Ordinal);
            foreach (string id in TypeIds)
            {
                builtins[$"{id}/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.";
                builtins[$"{id}/candidates/dev.md"] = "---\ntitle: Developer\nconsult_when: code changes\n---\nYou build things.";
                builtins[$"{id}/processes/flow.md"] = "# Flow";
            }

            builtins["gamma/team-type.json"] = GammaManifest;
            builtins["alpha/team-type.json"] = AlphaManifest;
            builtins["beta/team-type.json"] = BetaManifest;
            this.Types = new TeamTypeCatalog(this.Dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
            this.Packs = new SkillPackStore(this.Dir.Options(), NullLogger<SkillPackStore>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));
            this.avatars = new AvatarStore(this.Dir.Options(), NullLogger<AvatarStore>.Instance);
            HireLimitHarness harness = new(this.Dir.Options());
            this.Hire = new HireService(this.Personas, this.Types, this.Packs, this.avatars, harness.Guard, new FakeSkillInstaller(), new FakeStartGate(), NullLogger<HireService>.Instance);
            this.Folders = new TeamFolderProvisioner(this.Personas, this.library.RootStore, this.library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, this.Catalog);
        }

        public TempDataDir Dir { get; }

        public PersonaStore Personas { get; }

        public FakeTeamCatalog Catalog { get; }

        public TeamTypeCatalog Types { get; }

        public SkillPackStore Packs { get; }

        public HireService Hire { get; }

        public TeamFolderProvisioner Folders { get; }

        public MudBunitContext NewContext(ITeamFolders? folders = null)
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<ITeamFolders>(folders ?? this.Folders);
            ctx.Services.AddSingleton<ITeamCatalog>(this.Catalog);
            ctx.Services.AddSingleton(this.Types);
            ctx.Services.AddSingleton(this.Packs);
            ctx.Services.AddSingleton(this.Personas);
            ctx.Services.AddSingleton(this.Hire);
            ctx.Services.AddSingleton(this.hireContext);
            ctx.Services.AddSingleton(new ProcessProvisioner(this.Types, folders ?? this.Folders));
            return ctx;
        }

        public void Dispose()
        {
            this.avatars.Dispose();
            this.Personas.Dispose();
            this.library.Dispose();
            this.Dir.Dispose();
        }
    }
}
