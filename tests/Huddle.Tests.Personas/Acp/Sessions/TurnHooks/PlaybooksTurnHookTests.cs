using System.Text.Json;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Acp.Sessions.TurnHooks;

/// <summary>
/// Pins <see cref="PlaybooksTurnHook"/> (Team Playbooks spec §6.4): the state a session opens with, what a file edit, a Settings
/// save, a Reset, leaving a Team and losing the manager role tell the Agent, what <c>/compact</c> does and how a state that cannot be
/// read is treated. A real Persona store, catalog fake and Playbook files stand behind the hook.
/// </summary>
public sealed class PlaybooksTurnHookTests : IDisposable
{
    private const string RoomId = "room-1";
    private const string Header = "Your Playbooks changed since you last read them.";
    private const string DefaultV1 = "Default v1";
    private const string OwnHome = "Member Playbook for Home (this Team's own)";
    private const string DefaultHome = "Member Playbook (Huddle default) for Home";

    private static readonly TurnHookScope Scope = new("Vera", ReadsFiles: true, FileChangesOn: false, DeclaredWatches: []);

    private readonly TempDataDir dir = new();
    private readonly PersonaStore personas;
    private readonly FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Home", [], ["Vera"], true) { Manager = "Boss" }] };
    private readonly FakePromptSource prompts = new();
    private readonly CapturingLogger<PlaybooksTurnHook> logger = new();

    /// <summary>Opens a store holding the Persona <c>Vera</c> in the Team <c>Home</c>, with a short default Member Playbook.</summary>
    public PlaybooksTurnHookTests()
    {
        AgentPaths paths = new(this.dir.Options());
        Directory.CreateDirectory(Path.Combine(paths.DefinitionsRoot, "Vera"));
        File.WriteAllText(Path.Combine(paths.DefinitionsRoot, "Vera", "Vera.md"), PersonaText("Home"));
        this.personas = new PersonaStore(
            paths,
            new PersonaModelStore(this.dir.Options()),
            new PersonaEffortStore(this.dir.Options()),
            new PersonaWorkModeStore(this.dir.Options()),
            NullLogger<PersonaStore>.Instance);
        this.prompts.SetOverride("systemPrompt.memberPlaybook", DefaultV1);
        this.prompts.SetOverride("systemPrompt.managerPlaybook", "Manager default v1");
    }

    private string TeamsRoot
    {
        get
        {
            IOptions<TeamOptions> options = this.dir.Options();
            return Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.personas.Dispose();
        this.dir.Dispose();
    }

    /// <summary>The hook's name is the one the pipeline's production order lists.</summary>
    [Fact]
    public void HookName_IsPlaybooks()
    {
        Assert.Equal("Playbooks", PlaybooksTurnHook.HookName);
        Assert.Equal("Playbooks", this.NewHook().Name);
    }

    /// <summary>The hook runs for a normal session and not for a clean (goldfish) one.</summary>
    [Fact]
    public void AppliesTo_RunsUnlessTheScopeIsClean()
    {
        PlaybooksTurnHook hook = this.NewHook();

        Assert.True(hook.AppliesTo(Scope));
        Assert.False(hook.AppliesTo(Scope with { Clean = true }));
    }

    /// <summary>A fresh session of a member starts with, and has heard, the rules and the default of its Team, keyed as the spec names them.</summary>
    [Fact]
    public async Task Opening_Fresh_MemberKeys_StartedEqualsHeard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        string? state = await this.NewHook().OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);

        Assert.Equal(["rules:member", "member:default"], MapsOf(state).Started.Keys.ToList());
        Assert.Equal(MapsOf(state).Started, MapsOf(state).Heard);
    }

    /// <summary>A manager's fresh session also holds the manager rules and default; a Team's own file is keyed by the Team's name.</summary>
    [Fact]
    public async Task Opening_Fresh_ManagerWithOwnFile_KeysByTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.catalog.Teams = [new TeamSummary("Home", [], ["Vera"], true) { Manager = "Vera" }];
        this.WritePlaybook("Home", manager: false, "Own v1");

        string? state = await this.NewHook().OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);

        Assert.Equal(["rules:member", "member:Home", "rules:manager", "manager:default"], MapsOf(state).Started.Keys.ToList());
    }

    /// <summary>The state is JSON with exactly <c>started</c> and <c>heard</c>, and a resume reads back what was written.</summary>
    [Fact]
    public async Task State_RoundTripsAsJson_AndResumeKeepsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();

        string? written = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);
        string? read = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, written), ct);

        Assert.NotNull(written);
        using JsonDocument document = JsonDocument.Parse(written);
        Assert.Equal(["heard", "started"], document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToList());
        Assert.Equal(written, read);
    }

    /// <summary>A fresh session hears nothing on its first Turn: the map it opened with is the map it has now.</summary>
    [Fact]
    public async Task Before_FreshSession_HearsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await OpenAsync(hook, ct);

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.Null(addition);
    }

    /// <summary>A Turn after nothing changed returns no addition, however many Turns have run.</summary>
    [Fact]
    public async Task Before_UnchangedMap_ReturnsNoAddition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await OpenAsync(hook, ct);
        this.prompts.SetOverride("systemPrompt.memberPlaybook", "Default v2");
        (_, state) = await TurnAsync(hook, state, ct);

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.Null(addition);
    }

    /// <summary>
    /// Customizing the Team by a file edit tells the whole new text once under the changed line, headed once, and then nothing. The default
    /// that the file replaces is not told as gone: the Team still has a Playbook.
    /// </summary>
    [Fact]
    public async Task Before_FileEdit_TellsTheWholeNewTextOnce_ThenNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await OpenAsync(hook, ct);
        this.WritePlaybook("Home", manager: false, "Own v1");

        (TurnAddition? first, state) = await TurnAsync(hook, state, ct);
        TurnAddition? second = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.Equal(Block(Changed(OwnHome, "Own v1")), first?.Text);
        Assert.Null(second);
    }

    /// <summary>A second edit of the same file tells the second text, not the first, and not the heading twice.</summary>
    [Fact]
    public async Task Before_SecondEdit_TellsTheNewestText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        this.WritePlaybook("Home", manager: false, "Own v1");
        string? state = await OpenAsync(hook, ct);
        this.WritePlaybook("Home", manager: false, "Own v2");

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.Equal(Block(Changed(OwnHome, "Own v2")), addition?.Text);
    }

    /// <summary>A Settings save of the default Playbook reaches every session that reads it, each at its next Turn.</summary>
    [Fact]
    public async Task Before_SettingsSaveOfTheDefault_TellsEverySessionThatReadsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        string? first = await OpenAsync(hook, ct);
        string? second = await OpenAsync(hook, ct);
        this.prompts.SetOverride("systemPrompt.memberPlaybook", "Default v2");

        TurnAddition? toFirst = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, first), ct);
        TurnAddition? toSecond = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, second), ct);

        string expected = Block(Changed(DefaultHome, "Default v2"));
        Assert.Equal(expected, toFirst?.Text);
        Assert.Equal(expected, toSecond?.Text);
    }

    /// <summary>A Reset (the Team's file deleted) tells the Team now uses the default, with its text, and does not tell the file as gone.</summary>
    [Fact]
    public async Task Before_Reset_TellsTheDefaultText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        this.WritePlaybook("Home", manager: false, "Own v1");
        string? state = await OpenAsync(hook, ct);
        File.Delete(this.PathOf("Home", manager: false));

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.Equal(Block(Changed(DefaultHome, DefaultV1)), addition?.Text);
    }

    /// <summary>An Agent that leaves a Team with its own Playbook is told that Playbook no longer applies.</summary>
    [Fact]
    public async Task Before_LeavingATeam_TellsTheOwnPlaybookIsGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.catalog.Teams =
        [
            new TeamSummary("Home", [], ["Vera"], true) { Manager = "Boss" },
            new TeamSummary("Docs", [], ["Vera"], true) { Manager = "Boss" },
        ];
        _ = this.personas.Update("Vera", PersonaText("Home", "Docs"), null, null, null);
        this.WritePlaybook("Docs", manager: false, "Docs way");
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await OpenAsync(hook, ct);
        _ = this.personas.Update("Vera", PersonaText("Home"), null, null, null);

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.Equal(Block(Gone("Member Playbook for Docs (this Team's own)")), addition?.Text);
    }

    /// <summary>A manager that stops managing its Team is told both the Manager rules and the Manager Playbook no longer apply, rules first.</summary>
    [Fact]
    public async Task Before_LosingTheManagerRole_TellsManagerPartsAreGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.catalog.Teams = [new TeamSummary("Home", [], ["Vera"], true) { Manager = "Vera" }];
        this.WritePlaybook("Home", manager: true, "Manager own v1");
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await OpenAsync(hook, ct);
        this.catalog.Teams = [new TeamSummary("Home", [], ["Vera"], true) { Manager = "Boss" }];

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.Equal(
            Block(Gone("Huddle rules for managers"), Gone("Manager Playbook for Home (this Team's own)")),
            addition?.Text);
    }

    /// <summary>After <c>/compact</c> the session has heard only what it started with, so the next Turn tells every edit again, then nothing.</summary>
    [Fact]
    public async Task Compacted_ResetsHeardToStarted_SoTheEditIsToldAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await OpenAsync(hook, ct);
        string? startedState = state;
        this.prompts.SetOverride("systemPrompt.memberPlaybook", "Default v2");
        (TurnAddition? first, state) = await TurnAsync(hook, state, ct);

        state = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, state), ct);
        (TurnAddition? again, state) = await TurnAsync(hook, state, ct);
        TurnAddition? last = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.Equal(Block(Changed(DefaultHome, "Default v2")), first?.Text);
        Assert.Equal(first?.Text, again?.Text);
        Assert.Null(last);
        Assert.Equal(MapsOf(startedState).Started, MapsOf(state).Started);
    }

    /// <summary>A compacted state holds heard equal to started in the state itself, before any Turn.</summary>
    [Fact]
    public async Task Opening_Compacted_SetsHeardToStarted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();
        string? state = await OpenAsync(hook, ct);
        this.prompts.SetOverride("systemPrompt.memberPlaybook", "Default v2");
        (_, state) = await TurnAsync(hook, state, ct);
        Assert.NotEqual(MapsOf(state).Started, MapsOf(state).Heard);

        string? compacted = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, state), ct);

        Assert.Equal(MapsOf(state).Started, MapsOf(compacted).Heard);
    }

    /// <summary>A state that is not JSON is logged as a warning and treated as unknown: the Turn tells nothing, and After stores the current map.</summary>
    [Fact]
    public async Task Before_UnparseableState_LogsAndTreatsItAsUnknown()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybooksTurnHook hook = this.NewHook();

        (TurnAddition? addition, string? state) = await TurnAsync(hook, "{not json", ct);

        Assert.Contains(LogLevel.Warning, this.logger.Levels);
        Assert.NotNull(addition);
        Assert.Null(addition.Text);
        Assert.Equal(["rules:member", "member:default"], MapsOf(state).Heard.Keys.ToList());
    }

    private static string PersonaText(params string[] teams) =>
        $"---\nname: 'Vera'\ntitle: 'Vera'\nalias: 'vera'\nteams: [{string.Join(", ", teams.Select(static team => $"'{team}'"))}]\n---\nYou help.";

    private static string Changed(string playbook, string text) =>
        $"The {playbook} changed. It applies from now on and replaces what you read before:\n\n{text}";

    private static string Gone(string playbook) => $"The {playbook} no longer applies to you.";

    private static string Block(params string[] entries) => Header + "\n\n" + string.Join("\n\n", entries);

    private static Task<string?> OpenAsync(PlaybooksTurnHook hook, CancellationToken ct) =>
        hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);

    /// <summary>Runs Before and, when it returned an addition, After, as the pipeline does.</summary>
    private static async Task<(TurnAddition? Addition, string? State)> TurnAsync(PlaybooksTurnHook hook, string? state, CancellationToken ct)
    {
        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);
        if (addition is null)
        {
            return (null, state);
        }

        string? after = await ((ITurnHook)hook).AfterTurnAsync(new TurnEnded(Scope, RoomId, state, []), addition, ct);
        return (addition, after);
    }

    private static (Dictionary<string, string> Started, Dictionary<string, string> Heard) MapsOf(string? state)
    {
        Assert.NotNull(state);
        Dictionary<string, Dictionary<string, string>?>? maps = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>?>>(state);
        Assert.NotNull(maps);
        Dictionary<string, string>? started = maps.GetValueOrDefault("started");
        Dictionary<string, string>? heard = maps.GetValueOrDefault("heard");
        Assert.NotNull(started);
        Assert.NotNull(heard);
        return (started, heard);
    }

    private PlaybooksTurnHook NewHook() => new(this.personas, this.catalog, this.prompts, this.dir.Options(), this.logger);

    private string PathOf(string team, bool manager) => Path.Combine(this.TeamsRoot, team, manager ? "_manager-playbook.md" : "_member-playbook.md");

    private void WritePlaybook(string team, bool manager, string body)
    {
        string path = this.PathOf(team, manager);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.TeamsRoot);
        File.WriteAllText(path, body);
    }

    /// <summary>A logger that records the level of each entry.</summary>
    /// <typeparam name="T">The category.</typeparam>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        /// <summary>Gets the levels logged, in order.</summary>
        public List<LogLevel> Levels { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            this.Levels.Add(logLevel);
    }
}
