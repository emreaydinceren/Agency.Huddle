using System.Text.Json;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdTurnHook"/> (AgentsMdChanges Spec §8.5 and §6.10 "Telling again after /compact"): the state a session
/// opens with, what <c>/compact</c> does to it, the addition that lets After move the heard map, and a stored state that is read
/// back after the session was closed. A real ledger, source and Scopes stand behind the hook.
/// </summary>
public sealed class AgentsMdTurnHookTests : IDisposable
{
    private const string Relative = "Home/AGENTS.md";
    private const string RoomId = "room-1";

    private static readonly TurnHookScope Scope = new("Vera", ReadsFiles: true, FileChangesOn: false, DeclaredWatches: []);

    private readonly TempDataDir dir = new();
    private readonly PersonaStore personas;
    private readonly AgentsMdLedger ledger;
    private readonly CapturingLogger<AgentsMdTurnHook> logger = new();
    private int writes;

    /// <summary>Opens a store holding the Persona <c>Vera</c> in the Team <c>Home</c>, and a ledger over the temp folder.</summary>
    public AgentsMdTurnHookTests()
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
    }

    private string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

    /// <inheritdoc />
    public void Dispose()
    {
        this.personas.Dispose();
        this.dir.Dispose();
    }

    /// <summary>A fresh session starts with, and has heard, the hash of every file as it is on disk now.</summary>
    [Fact]
    public async Task Opening_Fresh_HashesStartedAndHeard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.Save(this.FileOf(Relative), 1);

        string? state = await this.NewHook().OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);

        AssertMap(MapsOf(state).Started, (Relative, HashOf(1)));
        AssertMap(MapsOf(state).Heard, (Relative, HashOf(1)));
    }

    /// <summary>A resumed session keeps the entry's maps untouched: they may hold the old prompt, and the files on disk are not read.</summary>
    [Fact]
    public async Task Opening_Resumed_KeepsTheEntrysMaps()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.Save(this.FileOf(Relative), 3);
        string stored = StateOf(Map(1), Map(2));

        string? state = await this.NewHook().OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, stored), ct);

        Assert.Equal(stored, state);
    }

    /// <summary>After <c>/compact</c> the session has heard only what it started with, and it still remembers where it started.</summary>
    [Fact]
    public async Task Opening_Compacted_SetsHeardToStarted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        string? state = await this.NewHook().OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, StateOf(Map(1), Map(3))), ct);

        AssertMap(MapsOf(state).Heard, (Relative, HashOf(1)));
        AssertMap(MapsOf(state).Started, (Relative, HashOf(1)));
    }

    /// <summary>A state from before the feature has no start to go back to: a compaction leaves it null, so it counts from first sight.</summary>
    [Fact]
    public async Task Opening_Compacted_NoStarted_StaysNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdTurnHook hook = this.NewHook();

        string? withoutNull = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, null), ct);
        string? withHeardOnly = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, StateOf(null, Map(2))), ct);

        Assert.Null(withoutNull);
        Assert.Null(withHeardOnly);
    }

    /// <summary>The state is JSON with exactly <c>started</c> and <c>heard</c>, and what a resume reads back is what was written.</summary>
    [Fact]
    public async Task State_RoundTripsAsJson()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.Save(this.FileOf(Relative), 1);
        AgentsMdTurnHook hook = this.NewHook();

        string? written = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);
        string? read = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, written), ct);

        Assert.NotNull(written);
        using JsonDocument document = JsonDocument.Parse(written);
        Assert.Equal(["heard", "started"], document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToList());
        Assert.Equal(written, read);
    }

    /// <summary>After writes the heard map Before carried and takes <c>started</c> from the Turn's state, so a Turn never forgets the start.</summary>
    [Fact]
    public async Task After_StoresTheCarriedHeard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ITurnHook hook = this.NewHook();
        TurnEnded ended = new(Scope, RoomId, StateOf(Map(1), Map(1)), []);

        string? state = await hook.AfterTurnAsync(ended, new TurnAddition(null, Map(3)), ct);

        AssertMap(MapsOf(state).Heard, (Relative, HashOf(3)));
        AssertMap(MapsOf(state).Started, (Relative, HashOf(1)));
    }

    /// <summary>
    /// A Turn that tells nothing because every revision was skipped still returns an addition, with no text: After runs only for
    /// an addition, and without it the heard map would never move past the skipped revision.
    /// </summary>
    [Fact]
    public async Task Before_SkippedOnly_ReturnsAnAddition_SoAfterMovesHeard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(file, 1);
        this.Save(file, 2);
        _ = this.ledger.Observe(file);
        _ = this.ledger.CommitVerdict(Relative, HashOf(1), HashOf(2), TextOf(2), new AgentsMdNotice.Skipped("typo"));
        ITurnHook hook = this.NewHook();
        string before = StateOf(Map(1), Map(1));

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, before), ct);
        string? after = await hook.AfterTurnAsync(new TurnEnded(Scope, RoomId, before, []), addition, ct);

        Assert.NotNull(addition);
        Assert.Null(addition.Text);
        AssertMap(MapsOf(after).Heard, (Relative, HashOf(2)));
        AssertMap(MapsOf(after).Started, (Relative, HashOf(1)));
    }

    /// <summary>After <c>/compact</c> the next Turn tells every notice since the session started, and the Turn after that tells nothing.</summary>
    [Fact]
    public async Task Compacted_NextTurnTellsEverythingSinceTheStart_ThenNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdFile file = this.FileOf(Relative);
        this.Save(file, 1);
        ITurnHook hook = this.NewHook();
        string? state = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);
        this.Play(file, 1, 2, 3);
        TurnAddition? first = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);
        state = await hook.AfterTurnAsync(new TurnEnded(Scope, RoomId, state, []), first, ct);

        state = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, state), ct);
        TurnAddition? again = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);
        state = await hook.AfterTurnAsync(new TurnEnded(Scope, RoomId, state, []), again, ct);
        TurnAddition? last = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.NotNull(first?.Text);
        Assert.Equal(first.Text, again?.Text);
        Assert.Null(last);
    }

    /// <summary>The state written at <c>/compact</c> is a string in the stored entry: a new hook that resumes from it still tells everything.</summary>
    [Fact]
    public async Task Compacted_SurvivesAClosedAndResumedSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdFile file = this.FileOf(Relative);
        this.Save(file, 1);
        AgentsMdTurnHook hook = this.NewHook();
        string? state = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);
        this.Play(file, 1, 2, 3);
        TurnAddition? first = await ((ITurnHook)hook).BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);
        state = await ((ITurnHook)hook).AfterTurnAsync(new TurnEnded(Scope, RoomId, state, []), first, ct);
        string? stored = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, state), ct);

        AgentsMdTurnHook reopened = this.NewHook();
        string? resumed = await reopened.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, stored), ct);
        TurnAddition? told = await ((ITurnHook)reopened).BeforeTurnAsync(new TurnStarting(Scope, RoomId, resumed), ct);

        Assert.NotNull(first?.Text);
        Assert.Equal(first.Text, told?.Text);
    }

    /// <summary>A state that is not the JSON of the two maps does not throw out of the hook: it counts as no state, and a Warning says so.</summary>
    [Fact]
    public async Task State_Corrupt_TreatedAsNull_AndWarns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.Save(this.FileOf(Relative), 1);
        AgentsMdTurnHook hook = this.NewHook();

        TurnAddition? addition = await hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, "{not json"), ct);
        string? compacted = await this.NewHook().OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, "[1,2]"), ct);

        Assert.NotNull(addition);
        Assert.Null(addition.Text);
        Assert.Null(compacted);
        Assert.Contains(LogLevel.Warning, this.logger.Levels);
    }

    /// <summary>Builds the hook over the real Scopes, ledger and source.</summary>
    private AgentsMdTurnHook NewHook()
    {
        AgentsMdScopes scopes = new(this.personas, new FakeTeamCatalog(), this.dir.Options());
        AgentsMdNoticeSource source = new(
            this.ledger,
            new AgentsMdNoticeRenderer(new FakePromptSource(), new AgentsMdOptions()),
            new AgentsMdOptions(),
            NullLogger<AgentsMdNoticeSource>.Instance);
        return new AgentsMdTurnHook(scopes, source, this.logger);
    }

    /// <summary>A Team-level file at <paramref name="relative"/> under the Teams root.</summary>
    /// <param name="relative">The ledger's relative path.</param>
    private AgentsMdFile FileOf(string relative) =>
        new(AgentsMdLevel.Team, "Home", null, Path.Combine(this.TeamsRoot, relative), relative);

    /// <summary>The text for version <paramref name="version"/>; each version has its own hash.</summary>
    /// <param name="version">The version number.</param>
    private static string TextOf(int version) => $"Rule {version}.\n";

    /// <summary>The hash the ledger uses for version <paramref name="version"/>.</summary>
    /// <param name="version">The version number.</param>
    private static string HashOf(int version) => AgentsMdBody.Parse(TextOf(version)).Hash;

    /// <summary>A map holding <see cref="Relative"/> at version <paramref name="version"/>.</summary>
    /// <param name="version">The version number.</param>
    private static Dictionary<string, string> Map(int version) => new() { [Relative] = HashOf(version) };

    /// <summary>The stored state for the two maps; a null map is left out, as for a session from before the feature.</summary>
    /// <param name="started">What the session started with, or null.</param>
    /// <param name="heard">What the session has heard.</param>
    private static string StateOf(Dictionary<string, string>? started, Dictionary<string, string> heard) =>
        started is null ? JsonSerializer.Serialize(new { heard }) : JsonSerializer.Serialize(new { started, heard });

    /// <summary>Reads the two maps out of a stored state.</summary>
    /// <param name="state">The state the hook wrote.</param>
    private static (Dictionary<string, string>? Started, Dictionary<string, string>? Heard) MapsOf(string? state)
    {
        Assert.NotNull(state);
        Dictionary<string, Dictionary<string, string>?>? maps = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>?>>(state);
        Assert.NotNull(maps);
        return (maps.GetValueOrDefault("started"), maps.GetValueOrDefault("heard"));
    }

    /// <summary>Asserts a map holds exactly the given pairs.</summary>
    /// <param name="map">The map.</param>
    /// <param name="expected">The path and hash pairs.</param>
    private static void AssertMap(Dictionary<string, string>? map, params (string Path, string Hash)[] expected)
    {
        Assert.NotNull(map);
        Assert.Equal(expected.Length, map.Count);
        foreach ((string path, string hash) in expected)
        {
            Assert.True(map.TryGetValue(path, out string? actual), $"'{path}' is missing from the map.");
            Assert.Equal(hash, actual);
        }
    }

    /// <summary>Writes a version with a distinct modification time.</summary>
    /// <param name="file">The file.</param>
    /// <param name="version">The version number.</param>
    private void Save(AgentsMdFile file, int version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath) ?? this.TeamsRoot);
        File.WriteAllText(file.FullPath, TextOf(version));
        this.writes++;
        File.SetLastWriteTimeUtc(file.FullPath, new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc).AddMinutes(this.writes));
    }

    /// <summary>Records the first version as first sight, then settles each later version as its own revision.</summary>
    /// <param name="file">The file.</param>
    /// <param name="versions">The versions in order; the first is the first sight.</param>
    private void Play(AgentsMdFile file, params int[] versions)
    {
        this.Save(file, versions[0]);
        _ = this.ledger.Observe(file);
        foreach (int version in versions.Skip(1))
        {
            this.Save(file, version);
            _ = this.ledger.Observe(file);
            _ = this.ledger.CommitComparison(file);
        }
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
