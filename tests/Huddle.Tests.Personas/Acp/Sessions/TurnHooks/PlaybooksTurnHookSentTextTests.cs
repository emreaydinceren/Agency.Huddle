using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp.Sessions.TurnHooks;

/// <summary>
/// Pins that <see cref="PlaybooksTurnHook"/> tells and hashes the text as the system prompt sends it (Team Playbooks spec §6.4): cut at
/// <c>MaxChars</c> with the pointer, headed with the Teams that share it, and logged once when the state cannot be read.
/// </summary>
public sealed class PlaybooksTurnHookSentTextTests : IDisposable
{
    private static readonly TurnHookScope Scope = new("Vera", ReadsFiles: true, FileChangesOn: false, DeclaredWatches: []);

    private readonly TempDataDir dir = new();
    private readonly PersonaStore personas;
    private readonly FakeTeamCatalog catalog = new();
    private readonly FakePromptSource prompts = new();
    private readonly CountingLogger logger = new();

    /// <summary>Opens a store holding the Persona <c>Vera</c> in the Teams <c>Ops</c> and <c>Sales</c>.</summary>
    public PlaybooksTurnHookSentTextTests()
    {
        AgentPaths paths = new(this.dir.Options());
        Directory.CreateDirectory(Path.Combine(paths.DefinitionsRoot, "Vera"));
        File.WriteAllText(Path.Combine(paths.DefinitionsRoot, "Vera", "Vera.md"), "---\nname: 'Vera'\ntitle: 'Vera'\nalias: 'vera'\nteams: ['Ops', 'Sales']\n---\nYou help.");
        this.personas = new PersonaStore(
            paths,
            new PersonaModelStore(this.dir.Options()),
            new PersonaEffortStore(this.dir.Options()),
            new PersonaWorkModeStore(this.dir.Options()),
            NullLogger<PersonaStore>.Instance);
        this.catalog.Teams =
        [
            new TeamSummary("Ops", [], ["Vera"], true) { Manager = "Boss" },
            new TeamSummary("Sales", [], ["Vera"], true) { Manager = "Boss" },
        ];
        this.prompts.SetOverride("systemPrompt.memberPlaybook", "Default v1");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.personas.Dispose();
        this.dir.Dispose();
    }

    /// <summary>A 30,000-character own Playbook is told cut at <c>MaxChars</c> with its pointer line, not whole.</summary>
    [Fact]
    public async Task Before_LongFileEdit_TellsTheCutTextWithItsPointer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);
        this.WritePlaybook("Ops", string.Concat(Enumerable.Repeat("A long line of Playbook text.\n", 1000)));

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, "room-1", state), ct);

        Assert.NotNull(addition);
        Assert.NotNull(addition.Text);
        int max = this.dir.Options().Value.Playbooks.MaxChars;
        Assert.True(addition.Text.Length < max + 1000);
        Assert.Contains("The rest of this Playbook is in `", addition.Text, StringComparison.Ordinal); // contains-ok: pins the pointer line is told.
    }

    /// <summary>
    /// Two Teams use the default and one is customized; Resetting the customized one is told as the default, headed with both Teams, because
    /// the heading is part of what the Agent read.
    /// </summary>
    [Fact]
    public async Task Before_ResetOfOneOfTwoTeams_TellsTheDefaultHeadedWithBoth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.WritePlaybook("Sales", "Sales way");
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);
        File.Delete(Path.Combine(this.TeamsRoot, "Sales", "_member-playbook.md"));

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, "room-1", state), ct);

        Assert.Equal(
            "Your Playbooks changed since you last read them.\n\nThe Member Playbook (Huddle default) for Ops and Sales changed. It applies from now on and replaces what you read before:\n\nDefault v1",
            addition?.Text);
    }

    /// <summary>A state that cannot be read is logged once for the Turn, not again when After stores the new state.</summary>
    [Fact]
    public async Task Turn_UnparseableState_LogsOneWarning()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, "room-1", "{not json"), ct);

        _ = await ((ITurnHook)hook).AfterTurnAsync(new TurnEnded(Scope, "room-1", "{not json", []), addition, ct);

        Assert.Equal(1, this.logger.Warnings);
    }

    /// <summary>An Agent that joins its first Team mid-session is told the Member rules with the tools named, not as unavailable.</summary>
    [Fact]
    public async Task Before_JoiningTheFirstTeam_TellsRulesNamingTheTools()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = this.personas.Update("Vera", "---\nname: 'Vera'\ntitle: 'Vera'\nalias: 'vera'\nteams: []\n---\nYou help.", null, null, null);
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);
        _ = this.personas.Update("Vera", "---\nname: 'Vera'\ntitle: 'Vera'\nalias: 'vera'\nteams: ['Ops']\n---\nYou help.", null, null, null);

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, "room-1", state), ct);

        Assert.NotNull(addition?.Text);
        Assert.Contains("add_comment", addition.Text, StringComparison.Ordinal); // contains-ok: pins a tool name in the told rules.
        Assert.DoesNotContain("(not available here; tell the Human)", addition.Text, StringComparison.Ordinal);
    }

    private string TeamsRoot
    {
        get
        {
            TeamOptions options = this.dir.Options().Value;
            return Path.GetFullPath(Path.Combine(options.DataDir, options.Teams.Dir));
        }
    }

    private PlaybooksTurnHook NewHook() => new(this.personas, this.catalog, this.prompts, this.dir.Options(), this.logger);

    private void WritePlaybook(string team, string body)
    {
        string path = Path.Combine(this.TeamsRoot, team, "_member-playbook.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.TeamsRoot);
        File.WriteAllText(path, body);
    }

    /// <summary>A logger that counts its warnings.</summary>
    private sealed class CountingLogger : ILogger<PlaybooksTurnHook>
    {
        /// <summary>Gets how many warnings were logged.</summary>
        public int Warnings { get; private set; }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                this.Warnings++;
            }
        }
    }
}
