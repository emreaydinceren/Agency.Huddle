using System.Text.Json;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdTurnHook"/> and a Project rename (Team Rename Spec 6.2): a heard Project-level <c>AGENTS.md</c> is re-keyed
/// to the Project's new place, so an open session sees the same file rather than a removal and an addition.
/// </summary>
public sealed class AgentsMdTurnHookProjectRenameTests : IDisposable
{
    private const string OldKey = "Home/Launch/AGENTS.md";
    private const string NewKey = "Home/Launch-2027/AGENTS.md";
    private const string Text = "Rule 1.\n";

    private static readonly TurnHookScope Scope = new("Vera", ReadsFiles: true, FileChangesOn: false, DeclaredWatches: []);

    private readonly TempDataDir dir = new();
    private readonly PersonaStore personas;
    private readonly AgentsMdLedger ledger;
    private readonly TeamRenameStore renames;

    /// <summary>Opens a store holding the Persona <c>Vera</c> in the Team <c>Home</c>, a ledger, and a rename journal.</summary>
    public AgentsMdTurnHookProjectRenameTests()
    {
        AgentPaths paths = new(this.dir.Options());
        Directory.CreateDirectory(Path.Combine(paths.DefinitionsRoot, "Vera"));
        File.WriteAllText(
            Path.Combine(paths.DefinitionsRoot, "Vera", "Vera.md"),
            "---\nname: 'Vera'\ntitle: 'Vera'\nalias: 'vera'\nteams: ['Home']\n---\nYou help.");
        this.personas = new PersonaStore(
            paths,
            new PersonaModelStore(this.dir.Options()),
            new PersonaEffortStore(this.dir.Options()),
            new PersonaWorkModeStore(this.dir.Options()),
            NullLogger<PersonaStore>.Instance);
        IOptions<TeamOptions> options = this.dir.Options();
        HelperSettingsResolver settings = new(new HelperSettingsStore(options), options, NullLogger<HelperSettingsResolver>.Instance);
        this.ledger = new AgentsMdLedger(
            static () => throw new InvalidOperationException("The dispatcher must not be reached."),
            static () => throw new InvalidOperationException("The Helper must not be reached."),
            settings,
            new HelperBudget(new HelperUsageStore(options)),
            options,
            NullLogger<AgentsMdLedger>.Instance);
        this.renames = new TeamRenameStore(options);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.personas.Dispose();
        this.dir.Dispose();
    }

    private static string HashOf() => AgentsMdBody.Parse(Text).Hash;

    private static string StateOf(string key) => JsonSerializer.Serialize(new
    {
        started = new Dictionary<string, string> { [key] = HashOf() },
        heard = new Dictionary<string, string> { [key] = HashOf() },
    });

    private AgentsMdTurnHook NewHook()
    {
        AgentsMdScopes scopes = new(this.personas, new FakeTeamCatalog(), this.dir.Options());
        AgentsMdNoticeSource source = new(
            this.ledger,
            new AgentsMdNoticeRenderer(new FakePromptSource(), new AgentsMdOptions()),
            new AgentsMdOptions(),
            NullLogger<AgentsMdNoticeSource>.Instance);
        return new AgentsMdTurnHook(scopes, source, NullLogger<AgentsMdTurnHook>.Instance, this.renames);
    }

    private void PutProjectFile(string project)
    {
        string folder = Path.Combine(this.dir.Path, "Teams", "Home", project);
        _ = Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "AGENTS.md"), Text);
    }

    private static Dictionary<string, string> HeardOf(TurnAddition? addition)
    {
        Assert.NotNull(addition);
        IReadOnlyDictionary<string, string> heard = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(addition.Carry);
        return heard.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
    }

    /// <summary>After a Done Project rename a heard Project AGENTS.md is the same file at its new path: no block, and the heard map holds only the new key.</summary>
    [Fact]
    public async Task Before_DoneProjectRename_KeepsTheHeardFileAsHeard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.renames.Complete(this.renames.Begin(TeamRenameKind.Project, "Home", "Launch", "Launch-2027"));
        this.PutProjectFile("Launch-2027");

        TurnAddition? addition = await ((ITurnHook)this.NewHook()).BeforeTurnAsync(new TurnStarting(Scope, "room-1", StateOf(OldKey)), ct);

        Assert.Null(addition?.Text);
        Assert.Equal(new Dictionary<string, string> { [NewKey] = HashOf() }, HeardOf(addition));
    }

    /// <summary>Re-keying is safe to repeat: a state already holding the new key tells nothing and changes nothing.</summary>
    [Fact]
    public async Task Before_DoneProjectRename_AlreadyRekeyed_TellsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.renames.Complete(this.renames.Begin(TeamRenameKind.Project, "Home", "Launch", "Launch-2027"));
        this.PutProjectFile("Launch-2027");

        TurnAddition? addition = await ((ITurnHook)this.NewHook()).BeforeTurnAsync(new TurnStarting(Scope, "room-1", StateOf(NewKey)), ct);

        Assert.Null(addition);
    }
}
