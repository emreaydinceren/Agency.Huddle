using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins the core of <see cref="TeamRenameService"/> (Team Rename Spec §5.2-5.5, R2-R4, R13, R14, as settled in
/// corrections-batch2 tr5): the refusals, the folder moves (also case-only and Archive), the store renames, the step-6 seam,
/// the label rewrite with its re-check, and the step-9 order. The fixture is the real folder provisioner, Task store, Persona
/// store, directory, id allocator, cadence store and journal over a temp data directory; the rewrites are recording fakes.
/// </summary>
public sealed class TeamRenameServiceTests
{
    private const string Marketing = "Marketing";
    private const string Growth = "Growth";

    /// <summary>R2: after the rename <c>Teams/Growth</c> holds the old content, <c>Teams/Marketing</c> is gone, every member says Growth and the journal row is Done.</summary>
    [Fact]
    public async Task RenameTeamAsync_ThreeMembers_FolderMovedLabelsRewrittenRowDone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder(Marketing));

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Assert.Null(outcome.Error);
        Assert.False(Directory.Exists(f.TeamFolder(Marketing)));
        Assert.Equal(before, Fixture.Snapshot(f.TeamFolder(Growth)));
        Assert.Contains("notes.md", before.Keys);
        Assert.Equal([Growth], f.Labels("Ada"));
        Assert.Equal([Growth, "Other"], f.Labels("Nova"));
        Assert.Equal([Growth], f.Labels("Bo"));
        TeamRename row = Assert.Single(f.Renames.DoneAfter(0));
        Assert.Equal((Marketing, Growth, TeamRenameState.Done), (row.OldName, row.NewName, row.State));
        Assert.Empty(f.Renames.Pending());
    }

    /// <summary>R3: six Rooms including the Team Room all end under Growth, none archived, exactly one Team Room, and the auto-named one is now called Growth.</summary>
    [Fact]
    public async Task RenameTeamAsync_SixRoomsWithTeamRoom_AllMoveNothingArchivedAndTeamRoomRenamed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        Room teamRoom = await f.CreateTeamRoomAsync(ct);
        await f.CreateOtherRoomsAsync(5, ct);
        Assert.Equal(6, (await f.Directory.GetRoomsForTeamAsync(Marketing, ct)).Count);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        IReadOnlyList<Room> rooms = await f.Directory.GetRoomsForTeamAsync(Growth, ct);
        Assert.Equal(6, rooms.Count);
        Assert.DoesNotContain(rooms, static r => r.Archived);
        Assert.Empty(await f.Directory.GetRoomsForTeamAsync(Marketing, ct));
        Room? now = await f.Directory.FindTeamRoomAsync(Growth, ct);
        Assert.NotNull(now);
        Assert.Equal(teamRoom.Id, now.Id);
        Assert.Equal(Growth, now.Name);
        Assert.Null(await f.Directory.FindTeamRoomAsync(Marketing, ct));
        Assert.Single(await f.Directory.GetRoomsAsync(ct), static r => r.Name == Growth);
    }

    /// <summary>R3: a Team Room the Human renamed by hand keeps its name through the rename.</summary>
    [Fact]
    public async Task RenameTeamAsync_HandRenamedTeamRoom_KeepsItsName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        Room teamRoom = await f.CreateTeamRoomAsync(ct);
        await f.Directory.RenameRoomAsync(teamRoom.Id, "Our own room", ct);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Room? now = await f.Directory.FindTeamRoomAsync(Growth, ct);
        Assert.NotNull(now);
        Assert.Equal(teamRoom.Id, now.Id);
        Assert.Equal("Our own room", now.Name);
    }

    /// <summary>R4: the Task prefix and counter are kept (the next id continues <c>MARK-</c>) and the cadence row moves to Growth.</summary>
    [Fact]
    public async Task RenameTeamAsync_PrefixAndCadence_KeptAndMoved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        TaskId first = f.Prefixes.Next(Marketing, 0);
        DateTimeOffset digest = new(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        f.Cadences.Set(new CadenceState(Marketing, digest, null, 3));

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        TaskId next = f.Prefixes.Next(Growth, 0);
        Assert.Equal((first.Prefix, first.Number + 1), (next.Prefix, next.Number));
        Assert.Equal(new CadenceState(Growth, digest, null, 3), f.Cadences.Get(Growth));
        Assert.Null(f.Cadences.Get(Marketing));
    }

    /// <summary>When <c>Archive/Marketing</c> exists it is moved to <c>Archive/Growth</c> with its content.</summary>
    [Fact]
    public async Task RenameTeamAsync_ArchiveFolderPresent_MovedToo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        string archived = Path.Combine(f.ArchiveRoot, Marketing);
        Directory.CreateDirectory(archived);
        File.WriteAllText(Path.Combine(archived, "old.md"), "archived project");

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Assert.False(Directory.Exists(archived));
        Assert.Equal("archived project", File.ReadAllText(Path.Combine(f.ArchiveRoot, Growth, "old.md")));
    }

    /// <summary>The step-6 seam: a rewrite runs once with the Pending row, after the folder move and before the labels, and the Teams it names are paused (so are the old and new names) while it runs.</summary>
    [Fact]
    public async Task RenameTeamAsync_Rewrite_RunsOnceBetweenMoveAndLabelsWithTeamsPaused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingRewrite rewrite = new(["Research"]);
        using Fixture f = await Fixture.CreateAsync(new Setup { Rewrites = [rewrite] }, ct);
        Directory.CreateDirectory(f.TeamFolder("Research"));
        (bool NewExists, bool OldExists, IReadOnlyList<string> Labels, bool ResearchPaused, bool GrowthPaused, bool MarketingPaused, bool ReconcileBlocked, int PendingRows) seen = default;
        Task? reconcile = null;
        rewrite.OnRun = _ =>
        {
            reconcile = f.Reconciler.ReconcileAsync(ct);
            seen = (
                Directory.Exists(f.TeamFolder(Growth)),
                Directory.Exists(f.TeamFolder(Marketing)),
                f.Labels("Ada"),
                f.IsPaused("Research"),
                f.IsPaused(Growth),
                f.IsPaused(Marketing),
                !reconcile.IsCompleted,
                f.Renames.Pending().Count);
        };

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Assert.NotNull(reconcile);
        await reconcile;
        TeamRename call = Assert.Single(rewrite.Calls);
        Assert.Equal((TeamRenameKind.Team, Marketing, Growth, TeamRenameState.Pending), (call.Kind, call.OldName, call.NewName, call.State));
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
        Assert.Equal(call.Id, f.Renames.DoneAfter(0)[0].Id);
        Assert.True(seen.NewExists);
        Assert.False(seen.OldExists);
        Assert.Equal([Marketing], seen.Labels);
        Assert.True(seen.ResearchPaused);
        Assert.True(seen.GrowthPaused);
        Assert.True(seen.MarketingPaused);
        Assert.True(seen.ReconcileBlocked);
        Assert.Equal(1, seen.PendingRows);
        Assert.False(f.IsPaused("Research"));
        Assert.False(f.IsPaused(Growth));
        Assert.Equal([Growth], f.Labels("Ada"));
    }

    /// <summary>Two rewrites run in registration order, each once.</summary>
    [Fact]
    public async Task RenameTeamAsync_TwoRewrites_RunInRegistrationOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> log = [];
        RecordingRewrite first = new([]) { OnRun = _ => log.Add("first") };
        RecordingRewrite second = new([]) { OnRun = _ => log.Add("second") };
        using Fixture f = await Fixture.CreateAsync(new Setup { Rewrites = [first, second] }, ct);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Assert.Equal(["first", "second"], log);
    }

    /// <summary>Step 9 order: when the row is marked Done the pauses and the reconciler suspension are still held; afterwards both are lifted.</summary>
    [Fact]
    public async Task RenameTeamAsync_Finish_MarksDoneBeforeLiftingPausesAndReconciler()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        (bool GrowthPaused, bool MarketingPaused, bool ReconcileBlocked) atComplete = default;
        Task? reconcile = null;
        f.Renames.OnComplete = () =>
        {
            reconcile = f.Reconciler.ReconcileAsync(ct);
            atComplete = (f.IsPaused(Growth), f.IsPaused(Marketing), !reconcile.IsCompleted);
        };

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Assert.True(atComplete.GrowthPaused);
        Assert.True(atComplete.MarketingPaused);
        Assert.True(atComplete.ReconcileBlocked);
        Assert.NotNull(reconcile);
        await reconcile;
        Assert.False(f.IsPaused(Growth));
        Assert.False(f.IsPaused(Marketing));
    }

    /// <summary>R13: renaming Growth to growth leaves the folder spelled <c>growth</c>, no temporary folder behind, and the labels rewritten.</summary>
    [Fact]
    public async Task RenameTeamAsync_CaseOnly_FolderRespelledNoTempLeftLabelsRewritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup { Team = Growth }, ct);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder(Growth));

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Growth, "growth", ct);

        Assert.True(outcome.Renamed);
        Assert.Null(outcome.Error);
        List<string> names = [.. Directory.GetDirectories(f.TeamsRoot).Select(static p => Path.GetFileName(p))];
        Assert.Contains("growth", names);
        Assert.DoesNotContain(Growth, names);
        Assert.DoesNotContain(names, static n => n.StartsWith(".huddle-renaming", StringComparison.Ordinal));
        Assert.Equal(before, Fixture.Snapshot(f.TeamFolder("growth")));
        Assert.Equal(["growth"], f.Labels("Ada"));
        Assert.Equal(TeamRenameState.Done, Assert.Single(f.Renames.DoneAfter(0)).State);
    }

    /// <summary>R14: each refused name gives a plain sentence and changes nothing (folder, labels, journal).</summary>
    [Theory]
    [InlineData("sales")]
    [InlineData("_Growth")]
    [InlineData("Gro,wth")]
    [InlineData(Marketing)]
    public async Task RenameTeamAsync_RefusedName_ErrorSetAndNothingChanged(string newName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, newName, ct);

        Assert.False(outcome.Renamed);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
        f.AssertUntouched();
    }

    /// <summary>R14: the name of another live Team is refused whatever its case, and the sentence names it.</summary>
    [Fact]
    public async Task RenameTeamAsync_NameOfLiveTeamInOtherCase_RefusedNamingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, "SALES", ct);

        Assert.False(outcome.Renamed);
        Assert.Contains("sales", outcome.Error, StringComparison.OrdinalIgnoreCase);
        f.AssertUntouched();
    }

    /// <summary>R14: the name of an archived Team (<c>Archive/&lt;T&gt;/_team.md</c>) is refused.</summary>
    [Fact]
    public async Task RenameTeamAsync_NameOfArchivedTeam_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        string legacy = Path.Combine(f.ArchiveRoot, "Legacy");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, TeamPaths.TeamFileName), "---\narchivedMembers: []\n---\n");

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, "legacy", ct);

        Assert.False(outcome.Renamed);
        Assert.Contains("legacy", outcome.Error, StringComparison.OrdinalIgnoreCase);
        f.AssertUntouched();
    }

    /// <summary>R14: a name held by the rename journal (here as the old name of an earlier rename) is refused.</summary>
    [Fact]
    public async Task RenameTeamAsync_ReservedJournalName_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        long earlier = f.Renames.Begin(TeamRenameKind.Team, "Alpha", "Alpha", "Beta");
        f.Renames.Complete(earlier);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, "alpha", ct);

        Assert.False(outcome.Renamed);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
        Assert.Equal(Marketing, f.Labels("Ada")[0]);
        Assert.True(Directory.Exists(f.TeamFolder(Marketing)));
        Assert.Empty(f.Renames.Pending());
        Assert.Single(f.Renames.DoneAfter(0));
    }

    /// <summary>R14: a leftover <c>task_prefixes</c> row held by the new name is refused, because its prefix and counter would collide.</summary>
    [Fact]
    public async Task RenameTeamAsync_LeftoverTaskPrefixRow_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        _ = f.Prefixes.PrefixFor("Ghost");

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, "Ghost", ct);

        Assert.False(outcome.Renamed);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
        f.AssertUntouched();
    }

    /// <summary>The source folder is in use: Renamed is false, the Error names the folder, <c>Teams/Marketing</c> is intact and the journal is empty.</summary>
    [Fact]
    public async Task RenameTeamAsync_SourceFolderInUse_RefusedAndNothingChanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem files = new(new PhysicalArchiveFileSystem()) { InUseFor = Growth };
        using Fixture f = await Fixture.CreateAsync(new Setup { Files = files }, ct);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.False(outcome.Renamed);
        Assert.Contains(Marketing, outcome.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.TeamFolder(Growth)));
        f.AssertUntouched();
    }

    /// <summary>The Teams folder moved but <c>Archive/Marketing</c> is in use: the Teams folder is moved back and the journal is empty.</summary>
    [Fact]
    public async Task RenameTeamAsync_ArchiveFolderInUse_TeamsFolderMovedBackJournalEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem files = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(new Setup { Files = files }, ct);
        string archived = Path.Combine(f.ArchiveRoot, Marketing);
        Directory.CreateDirectory(archived);
        File.WriteAllText(Path.Combine(archived, "old.md"), "archived project");
        int moves = 0;
        files.OnMove = () =>
        {
            moves++;
            if (moves == 2)
            {
                files.InUseFor = Growth;
            }
        };

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.False(outcome.Renamed);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
        Assert.False(Directory.Exists(f.TeamFolder(Growth)));
        Assert.True(File.Exists(Path.Combine(archived, "old.md")));
        f.AssertUntouched();
    }

    /// <summary>A label that will not rewrite: the membership is retried 3 more times (4 calls), then the row stays Pending and Renamed is false with an Error.</summary>
    [Fact]
    public async Task RenameTeamAsync_LabelWontRewrite_RetriedThenRowStaysPending()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup { NoOpMembership = true }, ct);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.False(outcome.Renamed);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
        Assert.Equal(4, f.Membership.RenameLabelCalls);
        TeamRename row = Assert.Single(f.Renames.Pending());
        Assert.Equal((Marketing, Growth), (row.OldName, row.NewName));
        Assert.Empty(f.Renames.DoneAfter(0));
        Assert.True(Directory.Exists(f.TeamFolder(Growth)));
    }

    /// <summary>A label rewrite that fails once and then works is retried and the rename completes.</summary>
    [Fact]
    public async Task RenameTeamAsync_LabelRewriteSucceedsOnRetry_Done()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup { SkipLabelRewrites = 1 }, ct);

        TeamRenameOutcome outcome = await f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.True(outcome.Renamed);
        Assert.Equal(2, f.Membership.RenameLabelCalls);
        Assert.Equal([Growth], f.Labels("Ada"));
        Assert.Empty(f.Renames.Pending());
        Assert.Single(f.Renames.DoneAfter(0));
    }

    /// <summary>Renames and archives share one gate: while it is held the rename waits, and it runs once the gate is released.</summary>
    [Fact]
    public async Task RenameTeamAsync_GateHeld_WaitsForIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(new Setup(), ct);
        IDisposable held = await f.Gate.EnterAsync(ct);

        Task<TeamRenameOutcome> pending = f.Service.RenameTeamAsync(Marketing, Growth, ct);

        Assert.False(pending.IsCompleted);
        Assert.True(Directory.Exists(f.TeamFolder(Marketing)));
        held.Dispose();
        Assert.True((await pending).Renamed);
    }

    /// <summary>A rewrite fake that records the renames it was run for and names the Teams it touches.</summary>
    /// <param name="touched">The Teams <see cref="TeamsTouched"/> returns.</param>
    private sealed class RecordingRewrite(IReadOnlyList<string> touched) : ITeamRenameRewrite
    {
        private readonly List<TeamRename> calls = [];

        /// <summary>Gets the renames it ran for, in order.</summary>
        public IReadOnlyList<TeamRename> Calls => this.calls;

        /// <summary>Gets or sets a callback run inside <see cref="RunAsync"/>.</summary>
        public Action<TeamRename>? OnRun { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<string> TeamsTouched(TeamRename rename) => touched;

        /// <inheritdoc />
        public Task RunAsync(TeamRename rename, CancellationToken ct)
        {
            this.calls.Add(rename);
            this.OnRun?.Invoke(rename);
            return Task.CompletedTask;
        }
    }

    /// <summary>The journal wrapped so a test can look at the world when <see cref="Complete"/> is called.</summary>
    /// <param name="inner">The real journal.</param>
    private sealed class HookedRenames(TeamRenameStore inner) : ITeamRenames
    {
        /// <summary>Gets or sets a callback run just before the row is completed.</summary>
        public Action? OnComplete { get; set; }

        /// <inheritdoc />
        public long Begin(TeamRenameKind kind, string team, string oldName, string newName) => inner.Begin(kind, team, oldName, newName);

        /// <inheritdoc />
        public void Complete(long id)
        {
            this.OnComplete?.Invoke();
            inner.Complete(id);
        }

        /// <inheritdoc />
        public void Discard(long id) => inner.Discard(id);

        /// <inheritdoc />
        public IReadOnlyList<TeamRename> Pending() => inner.Pending();

        /// <inheritdoc />
        public IReadOnlyList<TeamRename> DoneAfter(long id) => inner.DoneAfter(id);

        /// <inheritdoc />
        public IReadOnlyList<TeamRename> DoneSince(DateTimeOffset since) => inner.DoneSince(since);

        /// <inheritdoc />
        public long LatestDoneBefore(DateTimeOffset at) => inner.LatestDoneBefore(at);

        /// <inheritdoc />
        public string Resolve(string team) => inner.Resolve(team);

        /// <inheritdoc />
        public string ResolveProject(string team, string project) => inner.ResolveProject(team, project);

        /// <inheritdoc />
        public bool IsReserved(string name) => inner.IsReserved(name);

        /// <inheritdoc />
        public bool IsPending(string team) => inner.IsPending(team);

        /// <inheritdoc />
        public IReadOnlyList<string> EarlierNames(string team) => inner.EarlierNames(team);
    }

    /// <summary>A membership that counts <see cref="RenameLabel"/> calls, skips the first few (or all, when no-op) and otherwise rewrites through the real one, then flips the fake catalog.</summary>
    /// <param name="inner">The real membership.</param>
    /// <param name="catalog">The catalog that must follow the rename.</param>
    /// <param name="skip">How many calls change nothing; <see cref="int.MaxValue"/> for all.</param>
    private sealed class CountingMembership(ITeamMembership inner, FakeTeamCatalog catalog, int skip) : ITeamMembership
    {
        private int skipped;

        /// <summary>Gets how many times <see cref="RenameLabel"/> was called.</summary>
        public int RenameLabelCalls { get; private set; }

        /// <inheritdoc />
        public MembershipResult Add(string team, string personaName) => inner.Add(team, personaName);

        /// <inheritdoc />
        public MembershipResult Remove(string team, string personaName) => inner.Remove(team, personaName);

        /// <inheritdoc />
        public MembershipResult RemoveForArchive(string team, string personaName) => inner.RemoveForArchive(team, personaName);

        /// <inheritdoc />
        public MembershipResult ReplaceManager(string team, string newManager) => inner.ReplaceManager(team, newManager);

        /// <inheritdoc />
        public void RenameLabel(string oldName, string newName)
        {
            this.RenameLabelCalls++;
            if (this.skipped < skip)
            {
                this.skipped++;
                return;
            }

            inner.RenameLabel(oldName, newName);
            catalog.Teams = [.. catalog.Teams.Select(t => string.Equals(t.Name, oldName, StringComparison.OrdinalIgnoreCase) ? t with { Name = newName } : t)];
        }
    }

    /// <summary>What a test asks of the fixture.</summary>
    private sealed record Setup
    {
        /// <summary>Gets the Team to create and rename.</summary>
        public string Team { get; init; } = Marketing;

        /// <summary>Gets the file system the service moves with; <see langword="null"/> is the real disk.</summary>
        public IArchiveFileSystem? Files { get; init; }

        /// <summary>Gets the rewrites registered with the service, in order.</summary>
        public IReadOnlyList<ITeamRenameRewrite> Rewrites { get; init; } = [];

        /// <summary>Gets whether every <c>RenameLabel</c> changes nothing.</summary>
        public bool NoOpMembership { get; init; }

        /// <summary>Gets how many <c>RenameLabel</c> calls change nothing before one works.</summary>
        public int SkipLabelRewrites { get; init; }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly LibraryFileServiceFixture library;
        private readonly PersonaStore personas;
        private readonly TaskStore tasks;
        private readonly string team;
        private int probes;
        private readonly RoomEvents events = new(NullLogger<RoomEvents>.Instance);

        private Fixture(TempDataDir dir, LibraryFileServiceFixture library, PersonaStore personas, TaskStore tasks, SqliteTeamDirectory directory, FakeTeamCatalog catalog, TeamFolderProvisioner folders, Setup setup)
        {
            this.dir = dir;
            this.library = library;
            this.personas = personas;
            this.tasks = tasks;
            this.team = setup.Team;
            this.Directory = directory;
            this.Catalog = catalog;
            this.Prefixes = new TaskIdAllocator(dir.Options());
            this.Cadences = new CadenceStateStore(dir.Options());
            this.Renames = new HookedRenames(new TeamRenameStore(dir.Options()));
            this.Gate = new TeamOperationGate();
            this.Membership = new CountingMembership(
                new TeamMembership(personas, catalog, folders, RoleFixtures.Types(dir)),
                catalog,
                setup.NoOpMembership ? int.MaxValue : setup.SkipLabelRewrites);
            ChatService chat = new(
                directory,
                dir.CreateChatStore(),
                this.events,
                new FakeMentionAliasSource { Aliases = [] },
                TestOptions.Create(),
                new ProposalStore(this.events),
                new QuestionStore(this.events),
                NullLogger<ChatService>.Instance)
            {
                TeamCatalog = catalog,
            };
            this.Reconciler = new TeamRoomReconciler(directory, chat, catalog, this.events, NullLogger<TeamRoomReconciler>.Instance);
            this.TeamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            this.ArchiveRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Archive).FullPath;
            System.IO.Directory.CreateDirectory(this.ArchiveRoot);
            this.Service = new TeamRenameService(
                library.RootStore,
                catalog,
                this.Membership,
                folders,
                tasks,
                this.Prefixes,
                this.Cadences,
                personas,
                directory,
                setup.Files ?? new PhysicalArchiveFileSystem(),
                this.Renames,
                this.Reconciler,
                setup.Rewrites,
                this.Gate,
                NullLogger<TeamRenameService>.Instance);
        }

        public TeamRenameService Service { get; }

        public HookedRenames Renames { get; }

        public CountingMembership Membership { get; }

        public TeamOperationGate Gate { get; }

        public TaskIdAllocator Prefixes { get; }

        public CadenceStateStore Cadences { get; }

        public SqliteTeamDirectory Directory { get; }

        public FakeTeamCatalog Catalog { get; }

        public TeamRoomReconciler Reconciler { get; }

        public string TeamsRoot { get; }

        public string ArchiveRoot { get; }

        /// <summary>Builds the fixture: the Team and a Sales Team on disk, three members (Ada, Nova, Bo) and a Room database with their Users.</summary>
        /// <param name="setup">What the test asks for.</param>
        /// <param name="ct">Cancels the database set-up.</param>
        public static async Task<Fixture> CreateAsync(Setup setup, CancellationToken ct)
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = dir.CreatePersonaStore();
            FakeTeamCatalog catalog = new();
            TeamFolderProvisioner folders = new(personas, library.RootStore, library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog);
            string teamsRoot = library.RootStore.Roots.Single(r => r.Kind == LibraryRootKind.Teams).FullPath;
            TeamFile file = new(new VersionedRef("web", "1.2.0"), "Ada", null, null, string.Empty);
            Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam(setup.Team, file).Outcome);
            _ = personas.Add(new PersonaIdentity("Ada", "Lead", "ada", [setup.Team]), "Leads.");
            _ = personas.Add(new PersonaIdentity("Nova", "Dev", "nova", [setup.Team, "Other"]), "Builds.");
            _ = personas.Add(new PersonaIdentity("Bo", "Dev", "bo", [setup.Team]), "Helps.");
            string home = Path.Combine(teamsRoot, setup.Team);
            System.IO.Directory.CreateDirectory(Path.Combine(home, "memory"));
            File.WriteAllText(Path.Combine(home, "notes.md"), "# Notes\n");
            File.WriteAllText(Path.Combine(home, "memory", "lesson.md"), "a lesson");
            System.IO.Directory.CreateDirectory(Path.Combine(teamsRoot, "Sales"));
            catalog.Teams =
            [
                new TeamSummary(setup.Team, [], ["Ada", "Nova", "Bo"], HasFolder: true) { Manager = "Ada" },
                new TeamSummary("Sales", [], [], HasFolder: true),
            ];
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            foreach (string agent in new[] { "Ada", "Nova", "Bo" })
            {
                _ = await directory.UpsertAgentUserAsync(agent, null, ct);
            }

            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            return new Fixture(dir, library, personas, tasks, directory, catalog, folders, setup);
        }

        /// <summary>Lists every folder and file under <paramref name="root"/>, keyed by forward-slash relative path; a file maps to its hex bytes, a folder to a marker.</summary>
        /// <param name="root">The folder to list.</param>
        public static Dictionary<string, string> Snapshot(string root)
        {
            Dictionary<string, string> entries = new(StringComparer.Ordinal);
            foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                entries[relative] = System.IO.Directory.Exists(path) ? "<dir>" : Convert.ToHexString(File.ReadAllBytes(path));
            }

            return entries;
        }

        /// <summary>The path <c>Teams/&lt;name&gt;</c>.</summary>
        /// <param name="name">The folder name.</param>
        public string TeamFolder(string name) => Path.Combine(this.TeamsRoot, name);

        /// <summary>The Team labels in the named Persona's definition, as the store holds them now.</summary>
        /// <param name="name">The Persona's name.</param>
        public IReadOnlyList<string> Labels(string name)
        {
            Persona? persona = this.personas.Get(name);
            Assert.NotNull(persona);
            Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _));
            return identity.Teams;
        }

        /// <summary>Tells whether a Task write for the Team is refused now, by trying one.</summary>
        /// <param name="teamName">The Team.</param>
        public bool IsPaused(string teamName)
        {
            string id = string.Create(CultureInfo.InvariantCulture, $"PROBE-{++this.probes:0000}");
            TaskItem candidate = TestTasks.Make(id: id, location: new TaskLocation(teamName, null, false), path: string.Empty);
            try
            {
                _ = this.tasks.Create(candidate, TaskFileFormat.Compose(candidate));
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        /// <summary>Creates the Team Room through the reconciler, as the running app does.</summary>
        /// <param name="ct">Cancels the database work.</param>
        public async Task<Room> CreateTeamRoomAsync(CancellationToken ct)
        {
            await this.Reconciler.ReconcileAsync(ct);
            Room? room = await this.Directory.FindTeamRoomAsync(this.team, ct);
            Assert.NotNull(room);
            return room;
        }

        /// <summary>Creates Rooms of the Team with the Human and Ada in them.</summary>
        /// <param name="count">How many.</param>
        /// <param name="ct">Cancels the database writes.</param>
        public async Task CreateOtherRoomsAsync(int count, CancellationToken ct)
        {
            User? ada = await this.Directory.FindUserByNameAsync("Ada", ct);
            Assert.NotNull(ada);
            for (int i = 1; i <= count; i++)
            {
                _ = await this.Directory.CreateRoomAsync("Room " + i, [KnownIds.Human, ada.Id], this.team, false, ct: ct);
            }
        }

        /// <summary>Asserts the world is as the fixture built it: folder, labels, no journal row and no Growth folder.</summary>
        public void AssertUntouched()
        {
            Assert.True(File.Exists(Path.Combine(this.TeamFolder(this.team), "notes.md")));
            Assert.Equal([this.team], this.Labels("Ada"));
            Assert.Equal([this.team, "Other"], this.Labels("Nova"));
            Assert.Equal([this.team], this.Labels("Bo"));
            Assert.Empty(this.Renames.Pending());
            Assert.Empty(this.Renames.DoneAfter(0));
            Assert.DoesNotContain(
                System.IO.Directory.GetDirectories(this.TeamsRoot).Select(static p => Path.GetFileName(p)),
                static n => n.StartsWith(".huddle-renaming", StringComparison.Ordinal));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Reconciler.Dispose();
            this.tasks.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }
    }
}
