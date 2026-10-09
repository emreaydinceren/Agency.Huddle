using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Task 82 (HR-7, decision D6, Scenario 5) for <c>HiringPool</c>: a role that requires a connector is
/// Ready once <c>connectors.json</c> lists it; until then the card says Needs setup, names the connector,
/// shows where to add it, and offers an <c>I connected it</c> button that re-resolves the card.
/// </summary>
public sealed class HiringPoolConnectorTests
{
    private const string Manifest = """
        { "schemaVersion": 1, "id": "alpha", "version": "1.0.0", "displayName": "Alpha", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true, "suggestedNames": ["Ada"] },
            { "id": "mail", "title": "Liaison", "candidate": "candidates/mail.md", "requires": ["email:read"], "suggestedNames": ["Maya"] }
          ] }
        """;

    /// <summary>A connector listed in connectors.json makes the card Ready with no setup text.</summary>
    [Fact]
    public async Task Add_ConnectorListed_CardIsReady()
    {
        using Fixture fixture = new();
        File.WriteAllText(fixture.ConnectorsPath, """{"connected": ["email:read"]}""");
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await AddAsync(cut);

        Assert.Equal("Ready", cut.Find(".hiring-pool-hired-mail .hiring-pool-status").TextContent.Trim());
        Assert.Empty(cut.FindAll(".hiring-pool-connect"));
        Assert.Empty(cut.FindAll(".hiring-pool-connected"));
    }

    /// <summary>A missing connector shows Needs setup, names the connector, gives the file path and offers the button.</summary>
    [Fact]
    public async Task Add_ConnectorMissing_NeedsSetupNamingConnector()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await AddAsync(cut);

        Assert.Equal("Needs setup", cut.Find(".hiring-pool-hired-mail .hiring-pool-status").TextContent.Trim());
        Assert.Equal("Connect email:read to finish setup", cut.Find(".hiring-pool-hired-mail .hiring-pool-connect").TextContent.Trim());
        Assert.Equal($"Add \"email:read\" to the \"connected\" list in {fixture.ConnectorsPath}.", cut.Find(".hiring-pool-hired-mail .hiring-pool-connect-hint").TextContent.Trim());
        Assert.Equal("I connected it", cut.Find(".hiring-pool-hired-mail button.hiring-pool-connected").TextContent.Trim());
        Assert.Empty(cut.FindAll(".hiring-pool-hired-mail .hiring-pool-reason"));
    }

    /// <summary>A malformed connectors.json connects nothing, so the card still needs setup.</summary>
    [Fact]
    public async Task Add_MalformedFile_NeedsSetup()
    {
        using Fixture fixture = new();
        File.WriteAllText(fixture.ConnectorsPath, "{ not json");
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await AddAsync(cut);

        Assert.Equal("Needs setup", cut.Find(".hiring-pool-hired-mail .hiring-pool-status").TextContent.Trim());
        Assert.Equal("Connect email:read to finish setup", cut.Find(".hiring-pool-hired-mail .hiring-pool-connect").TextContent.Trim());
    }

    /// <summary>I connected it re-reads the file: still missing keeps Needs setup, then once listed the same card becomes Ready under the same name.</summary>
    [Fact]
    public async Task ConnectedButton_RereadsFile_ReResolvesCard()
    {
        using Fixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await AddAsync(cut);

        await cut.InvokeAsync(() => cut.Find("button.hiring-pool-connected").ClickAsync());
        Assert.Equal("Needs setup", cut.Find(".hiring-pool-hired-mail .hiring-pool-status").TextContent.Trim());

        File.WriteAllText(fixture.ConnectorsPath, """{"connected": ["email:read"]}""");
        await cut.InvokeAsync(() => cut.Find("button.hiring-pool-connected").ClickAsync());

        Assert.Equal("Ready", cut.Find(".hiring-pool-hired-mail .hiring-pool-status").TextContent.Trim());
        Assert.Equal("Maya", cut.Find(".hiring-pool-hired-mail .hiring-pool-name").TextContent.Trim());
        Assert.Empty(cut.FindAll(".hiring-pool-connect"));
        Assert.Equal(["Ada", "Maya"], cut.FindAll(".hiring-pool-hired .hiring-pool-name").Select(item => item.TextContent.Trim()));
    }

    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<HiringPool>(0);
            builder.AddAttribute(1, nameof(HiringPool.Team), "Alpha Team");
            builder.AddAttribute(2, nameof(HiringPool.TypeId), "alpha");
            builder.CloseComponent();
        });
    }

    private static async Task AddAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await cut.InvokeAsync(() => cut.Find("button.hiring-pool-add-mail").ClickAsync());
    }

    /// <summary>A hire context in which one adapter answered and connectors come from a real file.</summary>
    private sealed class FileContext(IConnectorStatus connectors) : IHireContext
    {
        public IReadOnlyList<AdapterAvailability> Answered { get; } = [new AdapterAvailability("claude-agent-acp", ["sonnet"])];

        public IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> TierMap { get; } =
            new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal)
            {
                ["claude-agent-acp"] = new Dictionary<ModelTier, string> { [ModelTier.Balanced] = "sonnet" },
            };

        public IReadOnlyCollection<string> AvoidAdapters { get; } = [];

        public IConnectorStatus Connectors { get; } = connectors;
    }

    /// <summary>The real stores behind the pool plus a file-backed connector status over a temp data folder.</summary>
    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            this.Dir = new TempDataDir();
            this.Personas = TestTaskStore.CreatePersonaStore(this.Dir);
            Dictionary<string, string> builtins = new(StringComparer.Ordinal)
            {
                ["alpha/team-type.json"] = Manifest,
                ["alpha/processes/flow.md"] = "# Flow",
                ["alpha/candidates/lead.md"] = "---\ntitle: Role\nconsult_when: work\n---\nYou work.",
                ["alpha/candidates/mail.md"] = "---\ntitle: Role\nconsult_when: work\n---\nYou work.",
            };
            this.Types = new TeamTypeCatalog(this.Dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
            this.Packs = new SkillPackStore(this.Dir.Options(), NullLogger<SkillPackStore>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));
            this.Folders = new FakeTeamFolders
            {
                ReadResult = new TeamFile(new VersionedRef("alpha", "1.0.0"), "Ada", null, null, "Process.md", string.Empty),
            };
            this.HireContext = new FileContext(new FileConnectorStatus(this.Dir.Options()));
        }

        public TempDataDir Dir { get; }

        public string ConnectorsPath => Path.Combine(this.Dir.Path, "connectors.json");

        public PersonaStore Personas { get; }

        public TeamTypeCatalog Types { get; }

        public SkillPackStore Packs { get; }

        public FakeTeamFolders Folders { get; }

        public IHireContext HireContext { get; }

        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<ITeamFolders>(this.Folders);
            ctx.Services.AddSingleton(this.Types);
            ctx.Services.AddSingleton(this.Packs);
            ctx.Services.AddSingleton(this.Personas);
            ctx.Services.AddSingleton(this.HireContext);
            return ctx;
        }

        public void Dispose()
        {
            this.Personas.Dispose();
            this.Dir.Dispose();
        }
    }
}
