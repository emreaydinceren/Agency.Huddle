using Bunit;
using Bunit.Rendering;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teammates;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Task 33 (HR-8, HR-9, NF-6, Screen 1): <c>TeamCheckBarView</c> renders the coverage chips
/// filled or empty, the vendor and process counts and the blockers; <c>TeamSetup</c> hosts the hiring
/// pool and the bar, enables Start Team only when nothing blocks it, starts the Team and goes to the
/// Team page, and shows a failed start in an alert while staying on the page; <c>TeamSpend</c> sums
/// the persisted spend of the members per currency for the Team page header.
/// </summary>
public sealed class TeamCheckBarViewTests
{
    private const string StartButton = "button.team-setup-start";

    private const string StartError = ".team-setup-start-error";

    private const string Manifest = """
        { "schemaVersion": 1, "id": "alpha", "version": "1.0.0", "displayName": "Alpha", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "coverage": ["generate", "challenge", "verify"],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true, "covers": ["generate"], "suggestedNames": ["Ada", "Grace"] },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 2, "covers": ["verify"], "suggestedNames": ["Dana", "Eli"] },
            { "id": "critic", "title": "Critic", "candidate": "candidates/critic.md", "covers": ["challenge"] }
          ] }
        """;

    /// <summary>A chip is filled for a covered step and empty for an uncovered one, labelled by the step.</summary>
    [Fact]
    public async Task View_CoverageChips_MatchCoverage()
    {
        await using MudBunitContext ctx = new();
        TeamCheck check = new(
            new Dictionary<string, bool>(StringComparer.Ordinal) { ["generate"] = true, ["challenge"] = false, ["verify"] = true },
            0, 0, true, []);

        IRenderedComponent<TeamCheckBarView> cut = ctx.Render<TeamCheckBarView>(p => p.Add(x => x.Check, check));

        Assert.Equal(["Generate", "Challenge", "Verify"], cut.FindAll(".team-check-chip").Select(item => item.TextContent.Trim()));
        Assert.Single(cut.FindAll(".team-check-chip-generate.team-check-chip-filled"));
        Assert.Single(cut.FindAll(".team-check-chip-challenge.team-check-chip-empty"));
        Assert.Single(cut.FindAll(".team-check-chip-verify.team-check-chip-filled"));
    }

    /// <summary>The bar says how many vendors and processes the Team would run (NF-6).</summary>
    [Fact]
    public async Task View_ShowsVendorAndProcessCounts()
    {
        await using MudBunitContext ctx = new();
        TeamCheck check = new(new Dictionary<string, bool>(StringComparer.Ordinal), 2, 3, true, []);

        IRenderedComponent<TeamCheckBarView> cut = ctx.Render<TeamCheckBarView>(p => p.Add(x => x.Check, check));

        Assert.Equal("2 vendors", cut.Find(".team-check-vendors").TextContent.Trim());
        Assert.Equal("3 processes", cut.Find(".team-check-processes").TextContent.Trim());
    }

    /// <summary>The blockers are listed one per item, and no list is drawn when there are none.</summary>
    [Fact]
    public async Task View_ListsBlockers()
    {
        await using MudBunitContext ctx = new();
        TeamCheck blocked = new(new Dictionary<string, bool>(StringComparer.Ordinal), 0, 1, false, ["Ravi needs setup: no adapter.", "The team needs a Lead."]);
        TeamCheck clear = new(new Dictionary<string, bool>(StringComparer.Ordinal), 0, 1, true, []);

        IRenderedComponent<TeamCheckBarView> withBlockers = ctx.Render<TeamCheckBarView>(p => p.Add(x => x.Check, blocked));
        IRenderedComponent<TeamCheckBarView> without = ctx.Render<TeamCheckBarView>(p => p.Add(x => x.Check, clear));

        Assert.Equal(["Ravi needs setup: no adapter.", "The team needs a Lead."], withBlockers.FindAll(".team-check-blockers li").Select(item => item.TextContent.Trim()));
        Assert.Empty(without.FindAll(".team-check-blockers"));
    }

    /// <summary>The setup page shows the pool and the bar: the manager covers its step and counts as one process.</summary>
    [Fact]
    public async Task Setup_ShowsPoolAndBarForTheManager()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        fixture.CreateTeam("Core");

        IRenderedComponent<ContainerFragment> cut = RenderSetup(ctx, "Core");

        Assert.Single(cut.FindAll(".hiring-pool"));
        Assert.Single(cut.FindAll(".team-check-chip-generate.team-check-chip-filled"));
        Assert.Single(cut.FindAll(".team-check-chip-verify.team-check-chip-empty"));
        Assert.Equal("1 processes", cut.Find(".team-check-processes").TextContent.Trim());
    }

    /// <summary>A hire added in the pool moves the bar: the covered step fills and the process count grows.</summary>
    [Fact]
    public async Task Setup_AddingARole_UpdatesTheBar()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        fixture.CreateTeam("Core");
        IRenderedComponent<ContainerFragment> cut = RenderSetup(ctx, "Core");

        await cut.InvokeAsync(() => cut.Find("button.hiring-pool-add-dev").ClickAsync());

        Assert.Single(cut.FindAll(".team-check-chip-verify.team-check-chip-filled"));
        Assert.Equal("2 processes", cut.Find(".team-check-processes").TextContent.Trim());
    }

    /// <summary>Start Team is disabled while a card needs setup, and the blocker is listed.</summary>
    [Fact]
    public async Task Setup_StartDisabled_WithBlocker()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        fixture.CreateTeam("Core");
        fixture.Context.Current = new NoAdapterContext();
        IRenderedComponent<ContainerFragment> cut = RenderSetup(ctx, "Core");

        await cut.InvokeAsync(() => cut.Find("button.hiring-pool-add-dev").ClickAsync());

        Assert.True(cut.Find(StartButton).HasAttribute("disabled"));
        Assert.Single(cut.FindAll(".team-check-blockers li"));
    }

    /// <summary>Start Team is enabled when nothing blocks it.</summary>
    [Fact]
    public async Task Setup_StartEnabled_WhenClear()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        fixture.CreateTeam("Core");

        IRenderedComponent<ContainerFragment> cut = RenderSetup(ctx, "Core");

        Assert.False(cut.Find(StartButton).HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(".team-check-blockers"));
    }

    /// <summary>Start Team hires the staged members and goes to the Team page.</summary>
    [Fact]
    public async Task Setup_Start_HiresAndNavigatesToTheTeam()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        fixture.CreateTeam("Core");
        IRenderedComponent<ContainerFragment> cut = RenderSetup(ctx, "Core");
        await cut.InvokeAsync(() => cut.Find("button.hiring-pool-add-dev").ClickAsync());

        await cut.InvokeAsync(() => cut.Find(StartButton).ClickAsync());

        Assert.Equal(2, fixture.Personas.ListNames().Count);
        Assert.Equal("http://localhost/teams/Core", ctx.Services.GetRequiredService<NavigationManager>().Uri);
    }

    /// <summary>A start that fails shows the error in an alert and stays on the setup page.</summary>
    [Fact]
    public async Task Setup_FailingStart_ShowsErrorAndStays()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        fixture.CreateTeam("Core");
        fixture.Gate.Running = false;
        IRenderedComponent<ContainerFragment> cut = RenderSetup(ctx, "Core");
        string before = ctx.Services.GetRequiredService<NavigationManager>().Uri;

        await cut.InvokeAsync(() => cut.Find(StartButton).ClickAsync());

        Assert.Equal("Ada did not start in time.", cut.Find(StartError).TextContent.Trim());
        Assert.Equal(before, ctx.Services.GetRequiredService<NavigationManager>().Uri);
    }

    /// <summary>The spend line sums the members' persisted spend per currency and is empty when nothing was spent.</summary>
    [Fact]
    public async Task Spend_SumsMembersPerCurrency()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());
        store.Add("Ada", null, "USD", 1.5m);
        store.Add("Dana", "T-1", "USD", 2m);
        store.Add("Dana", null, "EUR", 3m);
        store.Add("Outsider", null, "USD", 100m);
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(store);

        IRenderedComponent<TeamSpend> cut = ctx.Render<TeamSpend>(p => p.Add(x => x.Members, ["Ada", "Dana"]));
        IRenderedComponent<TeamSpend> none = ctx.Render<TeamSpend>(p => p.Add(x => x.Members, ["Nobody"]));

        Assert.Equal(["3.500 USD", "3.000 EUR"], cut.FindAll(".team-spend-amount").Select(item => item.TextContent.Trim()).Order(StringComparer.Ordinal).Reverse());
        Assert.Empty(none.FindAll(".team-spend"));
    }

    /// <summary>The Team page header hands the Team's members to the spend line.</summary>
    [Fact]
    public async Task TeamPage_Header_PassesMembersToSpend()
    {
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Business", [], ["Nova", "Kit"], true)] };
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton(Options.Create(new TeamOptions()));
        ctx.ComponentFactories.AddStub<TeamMembers>();
        ctx.ComponentFactories.AddStub<TeamFilesTab>();
        ctx.ComponentFactories.AddStub<TeamTasksTab>();
        ctx.ComponentFactories.AddStub<TeamSpend>();

        IRenderedComponent<TeamPage> cut = ctx.Render<TeamPage>(p => p.Add(x => x.Team, "Business"));

        Assert.Equal(["Nova", "Kit"], cut.FindComponent<Stub<TeamSpend>>().Instance.Parameters.Get(x => x.Members));
    }

    private static IRenderedComponent<ContainerFragment> RenderSetup(MudBunitContext ctx, string? team)
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

    /// <summary>A hire context in which no adapter answered, so every new card needs setup.</summary>
    private sealed class NoAdapterContext : IHireContext
    {
        public IReadOnlyList<AdapterAvailability> Answered { get; } = [];

        public IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> TierMap { get; } =
            new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> AvoidAdapters { get; } = [];

        public IConnectorStatus Connectors { get; } = new EmptyConnectorStatus();
    }

    /// <summary>A hire context that forwards to whichever context is current, so a test can change the answer mid-way.</summary>
    private sealed class SwitchContext : IHireContext
    {
        public IHireContext Current { get; set; } = new RoleFixtures.Context();

        public IReadOnlyList<AdapterAvailability> Answered => this.Current.Answered;

        public IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> TierMap => this.Current.TierMap;

        public IReadOnlyCollection<string> AvoidAdapters => this.Current.AvoidAdapters;

        public IConnectorStatus Connectors => this.Current.Connectors;
    }

    /// <summary>The real stores behind the page: one team type, a Persona store, the real folder provisioner and a hire service with a fake start gate.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly LibraryFileServiceFixture library;
        private readonly AvatarStore avatars;

        public Fixture()
        {
            this.Dir = new TempDataDir();
            this.library = LibraryFileServiceFixture.Attach(this.Dir.Path);
            this.Personas = TestTaskStore.CreatePersonaStore(this.Dir);
            this.Catalog = new FakeTeamCatalog();
            Dictionary<string, string> builtins = new(StringComparer.Ordinal)
            {
                ["alpha/team-type.json"] = Manifest,
                ["alpha/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
                ["alpha/candidates/dev.md"] = "---\ntitle: Developer\nconsult_when: code changes\n---\nYou build things.",
                ["alpha/candidates/critic.md"] = "---\ntitle: Critic\nconsult_when: reviews\n---\nYou push back.",
                ["alpha/processes/flow.md"] = "# Flow",
            };
            this.Types = new TeamTypeCatalog(this.Dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
            this.Packs = new SkillPackStore(this.Dir.Options(), NullLogger<SkillPackStore>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));
            this.avatars = new AvatarStore(this.Dir.Options(), NullLogger<AvatarStore>.Instance);
            HireLimitHarness harness = new(this.Dir.Options());
            this.Gate = new FakeStartGate();
            this.Hire = new HireService(this.Personas, this.Types, this.Packs, this.avatars, harness.Guard, new FakeSkillInstaller(), this.Gate, NullLogger<HireService>.Instance);
            this.Folders = new TeamFolderProvisioner(this.Personas, this.library.RootStore, this.library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, this.Catalog);
        }

        public TempDataDir Dir { get; }

        public PersonaStore Personas { get; }

        public FakeTeamCatalog Catalog { get; }

        public TeamTypeCatalog Types { get; }

        public SkillPackStore Packs { get; }

        public HireService Hire { get; }

        public FakeStartGate Gate { get; }

        public SwitchContext Context { get; } = new();

        public TeamFolderProvisioner Folders { get; }

        /// <summary>Creates the Team folder and hires its manager, as Create does, so the setup route shows the pool.</summary>
        public void CreateTeam(string name)
        {
            TeamTypeManifest type = this.Types.Find("alpha") ?? throw new InvalidOperationException("alpha missing");
            RoleSpec role = type.Roles.First(static r => r.Manager);
            TeamFile file = new(new VersionedRef(type.Id, type.Version), null, null, null, TeamPaths.ProcessFileName, string.Empty);
            _ = this.Folders.CreateTeam(name, file);
            CardState card = CardResolver.Resolve(role, this.Personas.ListNames(), this.Context.Answered, this.Context.TierMap, this.Context.AvoidAdapters, this.Packs.All, this.Context.Connectors);
            HireResult hired = this.Hire.Hire(new HireRequest(name, type.Id, role.Id, card));
            string manager = hired.PersonaName ?? throw new InvalidOperationException("manager not hired");
            _ = this.Folders.UpdateTeamFile(name, current => current with { Manager = manager });
        }

        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<ITeamFolders>(this.Folders);
            ctx.Services.AddSingleton<ITeamCatalog>(this.Catalog);
            ctx.Services.AddSingleton(this.Types);
            ctx.Services.AddSingleton(this.Packs);
            ctx.Services.AddSingleton(this.Personas);
            ctx.Services.AddSingleton(this.Hire);
            ctx.Services.AddSingleton<IHireContext>(this.Context);
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
