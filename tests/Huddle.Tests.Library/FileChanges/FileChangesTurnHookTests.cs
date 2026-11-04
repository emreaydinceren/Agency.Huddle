using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// Pins <see cref="FileChangesTurnHook"/> on its own, per Spec §6.7 "WorkItem" and §6.10 "Which steps use it": it renders
/// the same bytes as the block <c>RoomSession.AppendFileChangesBlock</c> writes today, applies exactly when File Changes
/// are on, carries the collected scan from Before to After, and owns the error handling the Room Session had.
/// </summary>
public sealed class FileChangesTurnHookTests
{
    private const string PersonaName = "Nova";

    /// <summary>
    /// A report with added, changed and deleted files, a by-you suffix, an unchecked folder and a not-listed count renders
    /// as the block the Room Session writes today, minus the two newlines that end it. The expected text is written out
    /// here rather than computed, so it still pins the bytes after the old method is deleted.
    /// </summary>
    [Fact]
    public void Render_MatchesTheOldBlock()
    {
        FileChangesReport report = new(
            [
                new FileChange(FileChangeKind.Added, "/w/new.md"),
                new FileChange(FileChangeKind.Changed, "/w/edited.md", "Planning"),
                new FileChange(FileChangeKind.Deleted, "/w/gone.md"),
            ],
            3,
            ["/w/big"],
            200);
        const string expected =
            "Since your last Turn in this Room, these files changed. Read one only if it matters to what you are doing now:\n"
            + "added /w/new.md\n"
            + "changed /w/edited.md (by you, in Room 'Planning')\n"
            + "deleted /w/gone.md\n"
            + "/w/big has more than 200 files, so it was not checked.\n"
            + "…and 3 more.\n\n";

        string? rendered = FileChangesTurnHook.Render(report, new FakePromptSource());

        Assert.Equal(expected, rendered + "\n\n");
    }

    /// <summary>An empty report renders nothing, as the old method wrote nothing.</summary>
    [Fact]
    public void Render_EmptyReport_ReturnsNull()
    {
        Assert.Null(FileChangesTurnHook.Render(FileChangesReport.Empty, new FakePromptSource()));
        Assert.Null(FileChangesTurnHook.Render(null, new FakePromptSource()));
    }

    /// <summary>The hook applies exactly when the scope says File Changes are on, which replaces the supervisor's check.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task AppliesTo_FollowsFileChangesOn(bool fileChangesOn, bool expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        TurnHookScope scope = new(PersonaName, true, fileChangesOn, []);

        bool applies = fixture.Hook.AppliesTo(scope);

        Assert.Equal(expected, applies);
    }

    /// <summary>A Room with no baseline yet gives no text, but still a carry so After commits the baseline.</summary>
    [Fact]
    public async Task BeforeTurn_FirstTurnInARoom_NullTextAndACarry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomId = await CreateRoomAsync(fixture, "RoomA", ct);

        TurnAddition? addition = await fixture.Hook.BeforeTurnAsync(new TurnStarting(Scope(), roomId, null), ct);

        Assert.NotNull(addition);
        Assert.Null(addition.Text);
        Assert.NotNull(addition.Carry);
    }

    /// <summary>
    /// After commits the start scan plus the paths touched during the Turn: a file the Agent wrote itself is not listed
    /// back on the next Turn, and one it did not write is.
    /// </summary>
    [Fact]
    public async Task AfterTurn_CommitsWithTheTouchedPaths()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomId = await CreateRoomAsync(fixture, "RoomA", ct);
        string workDir = Path.Combine(fixture.DataDir.Path, "Agents", PersonaName, "work");
        Directory.CreateDirectory(workDir);
        File.WriteAllText(Path.Combine(workDir, "start.md"), "start");
        TurnAddition? before = await fixture.Hook.BeforeTurnAsync(new TurnStarting(Scope(), roomId, null), ct);
        string touchedPath = Path.Combine(workDir, "touched.md");
        File.WriteAllText(touchedPath, "written during the Turn");
        string untouchedPath = Path.Combine(workDir, "untouched.md");
        File.WriteAllText(untouchedPath, "written by someone else");

        await ((ITurnHook)fixture.Hook).AfterTurnAsync(new TurnEnded(Scope(), roomId, null, [touchedPath]), before, ct);

        CollectedChanges next = await fixture.Tracker.CollectAsync(PersonaName, roomId, [], ct);
        FileChange change = Assert.Single(next.Report.Changes);
        Assert.Equal(untouchedPath, change.FullPath);
        Assert.Equal(FileChangeKind.Added, change.Kind);
    }

    /// <summary>A collect that throws is logged with today's message and gives no addition, so After does not run.</summary>
    [Fact]
    public async Task BeforeTurn_CollectThrows_LogsTheOldMessage_AndReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger<FileChangesTurnHook> logger = new();
        using Fixture fixture = await CreateFixtureAsync(ct, logger);

        TurnAddition? addition = await fixture.Hook.BeforeTurnAsync(new TurnStarting(Scope(), " ", null), ct);

        Assert.Null(addition);
        LogEntry entry = Assert.Single(logger.Entries, static candidate => candidate.Level == LogLevel.Warning);
        Assert.Equal($"Persona '{PersonaName}' failed to collect File Changes for room  .", entry.Message);
    }

    /// <summary>A commit that throws is logged with today's message and does not escape.</summary>
    [Fact]
    public async Task AfterTurn_CommitThrows_LogsTheOldMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger<FileChangesTurnHook> logger = new();
        using Fixture fixture = await CreateFixtureAsync(ct, logger);
        string roomId = await CreateRoomAsync(fixture, "RoomA", ct);
        TurnAddition? before = await fixture.Hook.BeforeTurnAsync(new TurnStarting(Scope(), roomId, null), ct);

        await ((ITurnHook)fixture.Hook).AfterTurnAsync(new TurnEnded(Scope(), " ", null, []), before, ct);

        LogEntry entry = Assert.Single(logger.Entries, static candidate => candidate.Level == LogLevel.Warning);
        Assert.Equal($"Persona '{PersonaName}' failed to commit File Changes for room  .", entry.Message);
    }

    /// <summary>The scope of the Persona under test: reads files, File Changes on, no declared watches.</summary>
    private static TurnHookScope Scope() => new(PersonaName, true, true, []);

    /// <summary>Creates a real Room through the fixture's directory, returning its id.</summary>
    private static async Task<string> CreateRoomAsync(Fixture fixture, string name, CancellationToken ct)
    {
        Room room = await fixture.Directory.CreateRoomAsync(name, [KnownIds.Human], ct);
        return room.Id;
    }

    /// <summary>
    /// Builds a real tracker over a temp data directory holding a Persona "Nova", as <c>FileChangeTrackerTests</c> does,
    /// and the hook under test over it.
    /// </summary>
    private static async Task<Fixture> CreateFixtureAsync(CancellationToken ct, ILogger<FileChangesTurnHook>? logger = null)
    {
        TempDataDir dataDir = new();
        IOptions<TeamOptions> options = dataDir.Options();
        AgentPaths agentPaths = new(options);
        TestPersonaFiles.Write(agentPaths, "nova", "---\nName: Nova\nTitle: Nova\nAlias: Nova\n---\nYou are Nova.");

        SqliteTeamDirectory directory = new(options);
        await directory.InitializeAsync("You", ct);

        PersonaStore personas = new(
            agentPaths,
            new PersonaModelStore(options),
            new PersonaEffortStore(options),
            new PersonaWorkModeStore(options),
            NullLogger<PersonaStore>.Instance);
        FileStateStore store = new(options, NullLogger<FileStateStore>.Instance);
        WatchedFolderResolver resolver = new(options, agentPaths);
        FileChangeTracker tracker = new(store, personas, directory, resolver, options, agentPaths, NullLogger<FileChangeTracker>.Instance);
        FileChangesTurnHook hook = new(tracker, new FakePromptSource(), logger ?? NullLogger<FileChangesTurnHook>.Instance);

        return new Fixture(dataDir, directory, personas, tracker, hook);
    }

    /// <summary>The real collaborators one hook under test needs, torn down together.</summary>
    private sealed class Fixture(TempDataDir dataDir, SqliteTeamDirectory directory, PersonaStore personas, FileChangeTracker tracker, FileChangesTurnHook hook) : IDisposable
    {
        /// <summary>Gets the isolated temp data directory backing this fixture.</summary>
        public TempDataDir DataDir { get; } = dataDir;

        /// <summary>Gets the directory that holds this fixture's Rooms.</summary>
        public SqliteTeamDirectory Directory { get; } = directory;

        /// <summary>Gets the tracker the hook wraps, for reading what a commit saved.</summary>
        public FileChangeTracker Tracker { get; } = tracker;

        /// <summary>Gets the hook under test.</summary>
        public FileChangesTurnHook Hook { get; } = hook;

        /// <inheritdoc/>
        public void Dispose()
        {
            personas.Dispose();
            this.DataDir.Dispose();
        }
    }
}
