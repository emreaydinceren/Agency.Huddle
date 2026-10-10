using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teammates;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Task 32 (HR-3 to HR-6, Screen 1) for <c>HiringPool</c>: one pool card per role with its
/// suggested name, a click on <c>+</c> staging a hire whose name never repeats, a role at its limit
/// greyed and disabled, a preset adding its roles in one step while skipping full ones, each staged
/// card showing Ready or Needs setup with its reasons, and the manager card having no remove button.
/// Drag-and-drop is not offered; the <c>+</c> button is the only way to hire.
/// </summary>
public sealed class HiringPoolTests
{
    private const string Manifest = """
        { "schemaVersion": 1, "id": "alpha", "version": "1.0.0", "displayName": "Alpha", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "presets": [{ "id": "crew", "label": "Full crew", "roles": ["lead", "dev", "qa", "ops", "writer"] }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true, "covers": ["plan"], "suggestedNames": ["Ada", "Grace"] },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 2, "covers": ["build", "review"], "suggestedNames": ["Dana", "Eli"] },
            { "id": "qa", "title": "Tester", "candidate": "candidates/qa.md", "suggestedNames": ["Quinn"] },
            { "id": "ops", "title": "Operator", "candidate": "candidates/ops.md", "suggestedNames": ["Orla"] },
            { "id": "writer", "title": "Writer", "candidate": "candidates/writer.md", "suggestedNames": ["Wren"] }
          ] }
        """;

    private static readonly string[] RoleIds = ["lead", "dev", "qa", "ops", "writer"];

    /// <summary>The pool shows one card per role with its title and the first suggested name still free.</summary>
    [Fact]
    public async Task Pool_ShowsOneCardPerRoleWithSuggestedName()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal(["Lead", "Developer", "Tester", "Operator", "Writer"], cut.FindAll(".hiring-pool-card .hiring-pool-title").Select(item => item.TextContent.Trim()));
        Assert.Equal(["Grace", "Dana", "Quinn", "Orla", "Wren"], cut.FindAll(".hiring-pool-card .hiring-pool-suggested").Select(item => item.TextContent.Trim()));
    }

    /// <summary>Clicking + on a role stages a card in the Team area with the suggested name.</summary>
    [Fact]
    public async Task Add_StagesCardWithSuggestedName()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await AddAsync(cut, "dev");

        Assert.Equal(["Ada", "Dana"], cut.FindAll(".hiring-pool-hired .hiring-pool-name").Select(item => item.TextContent.Trim()));
    }

    /// <summary>A second Developer gets a different name from the first.</summary>
    [Fact]
    public async Task Add_SecondDeveloper_GetsDifferentName()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await AddAsync(cut, "dev");
        await AddAsync(cut, "dev");

        Assert.Equal(["Ada", "Dana", "Eli"], cut.FindAll(".hiring-pool-hired .hiring-pool-name").Select(item => item.TextContent.Trim()));
    }

    /// <summary>A role at its limit has a disabled + and says the limit is reached.</summary>
    [Fact]
    public async Task FullRole_IsDisabledAndSaysLimitReached()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.False(cut.Find("button.hiring-pool-add-dev").HasAttribute("disabled"));
        await AddAsync(cut, "dev");
        await AddAsync(cut, "dev");

        Assert.True(cut.Find("button.hiring-pool-add-dev").HasAttribute("disabled"));
        Assert.Equal("Limit reached", cut.Find(".hiring-pool-card-dev .hiring-pool-limit").TextContent.Trim());
        Assert.True(cut.Find("button.hiring-pool-add-lead").HasAttribute("disabled"));
    }

    /// <summary>A preset adds its roles at once: the four open ones, skipping the manager role that is already hired.</summary>
    [Fact]
    public async Task Preset_AddsFourCardsSkippingFullRoles()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.Find("button.hiring-pool-preset-crew").ClickAsync());

        Assert.Equal("Full crew", cut.Find("button.hiring-pool-preset-crew").TextContent.Trim());
        Assert.Equal(["Ada", "Dana", "Quinn", "Orla", "Wren"], cut.FindAll(".hiring-pool-hired .hiring-pool-name").Select(item => item.TextContent.Trim()));
        Assert.Equal(5, cut.FindAll(".hiring-pool-hired .hiring-pool-name").Count);
    }

    /// <summary>A staged card that needs setup shows its status and every reason; a Ready card shows Ready.</summary>
    [Fact]
    public async Task NeedsSetup_ShowsStatusAndReasons()
    {
        using Fixture ready = new();
        await using MudBunitContext readyCtx = ready.NewContext();
        IRenderedComponent<ContainerFragment> readyCut = Render(readyCtx);
        await AddAsync(readyCut, "dev");
        Assert.Equal("Ready", readyCut.Find(".hiring-pool-hired-dev .hiring-pool-status").TextContent.Trim());

        using Fixture fixture = new(new NoAdapterContext());
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await AddAsync(cut, "dev");

        TeamTypeManifest type = Assert.IsType<TeamTypeManifest>(fixture.Types.Find("alpha"));
        CardState expected = CardResolver.Resolve(type.Roles[1], [], [], new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal), [], [], new EmptyConnectorStatus());
        Assert.Equal("Needs setup", cut.Find(".hiring-pool-hired-dev .hiring-pool-status").TextContent.Trim());
        Assert.Equal(expected.Reasons, cut.FindAll(".hiring-pool-hired-dev .hiring-pool-reason").Select(item => item.TextContent.Trim()));
    }

    /// <summary>The pool and Team columns are headed, only the locked manager card carries the Manager cue, and a card that needs setup is marked so its border can say so.</summary>
    [Fact]
    public async Task Layout_HeadsBothColumnsMarksManagerAndNeedsSetup()
    {
        using Fixture fixture = new(new NoAdapterContext());
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await AddAsync(cut, "dev");

        Assert.Equal(["Hiring pool", "Team"], cut.FindAll(".hiring-pool-heading").Select(item => item.TextContent.Trim()));
        Assert.Equal(["Manager"], cut.FindAll(".hiring-pool-locked").Select(item => item.TextContent.Trim()));
        Assert.Empty(cut.FindAll(".hiring-pool-hired-dev .hiring-pool-locked"));
        Assert.Single(cut.FindAll(".hiring-pool-hired-dev.hiring-pool-hired-needs-setup"));
    }

    /// <summary>A staged card can be removed before Start; the manager card has no remove button.</summary>
    [Fact]
    public async Task Remove_StagedCardOnly_ManagerHasNoRemoveButton()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await AddAsync(cut, "dev");

        Assert.Empty(cut.FindAll(".hiring-pool-hired-lead button.hiring-pool-remove"));
        Assert.Single(cut.FindAll(".hiring-pool-hired-dev button.hiring-pool-remove"));

        await cut.InvokeAsync(() => cut.Find(".hiring-pool-hired-dev button.hiring-pool-remove").ClickAsync());

        Assert.Equal(["Ada"], cut.FindAll(".hiring-pool-hired .hiring-pool-name").Select(item => item.TextContent.Trim()));
        Assert.False(cut.Find("button.hiring-pool-add-dev").HasAttribute("disabled"));
    }

    /// <summary>The pool ends with one Custom role card whose button is not counted among the role cards.</summary>
    [Fact]
    public async Task Pool_EndsWithCustomRoleCard()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("Custom role", cut.Find(".hiring-pool-custom .hiring-pool-custom-title").TextContent.Trim());
        Assert.Equal("Hire a custom role", cut.Find("button.hiring-pool-custom-add").GetAttribute("aria-label"));
        Assert.Equal(5, cut.FindAll(".hiring-pool-card").Count);
    }

    /// <summary>A custom role title becomes a lower-case hyphenated role id.</summary>
    /// <param name="title">The title typed by the user.</param>
    /// <param name="expected">The role id derived from it.</param>
    [Theory]
    [InlineData("Research Analyst", "research-analyst")]
    [InlineData("  It's a  Role! ", "it-s-a-role")]
    [InlineData("!!!", "")]
    public void CustomRoleDialog_Slug_BuildsRoleId(string title, string expected)
    {
        Assert.Equal(expected, CustomRoleDialog.Slug(title));
    }

    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Teams.HiringPool>(0);
            builder.AddAttribute(1, nameof(Agency.Huddle.App.Components.Teams.HiringPool.Team), "Alpha Team");
            builder.AddAttribute(2, nameof(Agency.Huddle.App.Components.Teams.HiringPool.TypeId), "alpha");
            builder.CloseComponent();
        });
    }

    private static async Task AddAsync(IRenderedComponent<ContainerFragment> cut, string roleId)
    {
        await cut.InvokeAsync(() => cut.Find($"button.hiring-pool-add-{roleId}").ClickAsync());
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

    /// <summary>The real stores behind the pool: one team type, a Persona store and a Team file whose manager is already hired.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly IHireContext hireContext;

        public Fixture(IHireContext? hireContext = null)
        {
            this.hireContext = hireContext ?? new RoleFixtures.Context();
            this.Dir = new TempDataDir();
            this.Personas = TestTaskStore.CreatePersonaStore(this.Dir);
            Dictionary<string, string> builtins = new(StringComparer.Ordinal)
            {
                ["alpha/team-type.json"] = Manifest,
                ["alpha/processes/flow.md"] = "# Flow",
            };
            foreach (string id in RoleIds)
            {
                builtins[$"alpha/candidates/{id}.md"] = "---\ntitle: Role\nconsult_when: work\n---\nYou work.";
            }

            this.Types = new TeamTypeCatalog(this.Dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
            this.Packs = new SkillPackStore(this.Dir.Options(), NullLogger<SkillPackStore>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));
            this.Folders = new FakeTeamFolders
            {
                ReadResult = new TeamFile(new VersionedRef("alpha", "1.0.0"), "Ada", null, null, "Process.md", string.Empty),
            };
        }

        public TempDataDir Dir { get; }

        public PersonaStore Personas { get; }

        public TeamTypeCatalog Types { get; }

        public SkillPackStore Packs { get; }

        public FakeTeamFolders Folders { get; }

        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<ITeamFolders>(this.Folders);
            ctx.Services.AddSingleton(this.Types);
            ctx.Services.AddSingleton(this.Packs);
            ctx.Services.AddSingleton(this.Personas);
            ctx.Services.AddSingleton(this.hireContext);
            return ctx;
        }

        public void Dispose()
        {
            this.Personas.Dispose();
            this.Dir.Dispose();
        }
    }
}
