using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// Pins <see cref="FileChangeTracker"/>'s per-Room collect/commit semantics, per FC §6.7, use
/// cases F0, F4a, F7, F10 and FC §9's E-1 through E-4.
/// </summary>
public sealed class FileChangeTrackerTests
{
    /// <summary>A Room with no baseline yet returns an empty report (F7, E-1).</summary>
    [Fact]
    public async Task Collect_FirstTurnInRoom_ReturnsEmptyReport()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        Assert.True(collected.Report.IsEmpty);
    }

    /// <summary>A file changed after a commit, before the next collect, is listed next time.</summary>
    [Fact]
    public async Task CollectCommitCollect_UntouchedChangeBetweenTurns_IsListed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string workDir = WorkDir(fixture);
        Directory.CreateDirectory(workDir);
        string filePath = Path.Combine(workDir, "a.md");
        File.WriteAllText(filePath, "v1");

        CollectedChanges first = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomA, first, [], ct);

        File.WriteAllText(filePath, "v2 longer");
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));

        CollectedChanges second = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        FileChange change = Assert.Single(second.Report.Changes);
        Assert.Equal(FileChangeKind.Changed, change.Kind);
        Assert.Equal(filePath, change.FullPath);
    }

    /// <summary>F0: an edit committed in Room A is not listed back in A, but is listed in Room B.</summary>
    [Fact]
    public async Task Commit_TouchedEditInRoomA_NotListedInA_ListedInB()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string roomB = await CreateRoomAsync(fixture, "RoomB", ct);

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomB, await fixture.Tracker.CollectAsync("Nova", roomB, [], ct), [], ct);

        CollectedChanges collectedA = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        string memoryDir = Path.Combine(WorkDir(fixture), "memory");
        Directory.CreateDirectory(memoryDir);
        string filePath = Path.Combine(memoryDir, "launch-date.md");
        File.WriteAllText(filePath, "The launch date is 2026-10-01.");

        await fixture.Tracker.CommitAsync("Nova", roomA, collectedA, [filePath], ct);

        CollectedChanges nextA = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        Assert.True(nextA.Report.IsEmpty);

        CollectedChanges nextB = await fixture.Tracker.CollectAsync("Nova", roomB, [], ct);
        FileChange change = Assert.Single(nextB.Report.Changes);
        Assert.Equal(FileChangeKind.Added, change.Kind);
        Assert.Equal(filePath, change.FullPath);
    }

    /// <summary>F4a: a file changed by someone else during the Turn, not passed in touched, is still listed next time.</summary>
    [Fact]
    public async Task Commit_UntouchedChangeDuringTurn_ListedInSameRoomNextTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string workDir = WorkDir(fixture);
        Directory.CreateDirectory(workDir);
        string filePath = Path.Combine(workDir, "a.md");
        File.WriteAllText(filePath, "v1");
        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        File.WriteAllText(filePath, "v2 longer, someone else's write");
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));

        await fixture.Tracker.CommitAsync("Nova", roomA, collected, [], ct);

        CollectedChanges next = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        FileChange change = Assert.Single(next.Report.Changes);
        Assert.Equal(FileChangeKind.Changed, change.Kind);
    }

    /// <summary>E-1a: two edits to the same file inside Room A are still listed once in Room B.</summary>
    [Fact]
    public async Task Collect_TwoEditsInA_ListedOnceInB()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string roomB = await CreateRoomAsync(fixture, "RoomB", ct);
        string workDir = WorkDir(fixture);
        Directory.CreateDirectory(workDir);
        string filePath = Path.Combine(workDir, "a.md");
        File.WriteAllText(filePath, "v1");

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomB, await fixture.Tracker.CollectAsync("Nova", roomB, [], ct), [], ct);

        CollectedChanges collectedA1 = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        File.WriteAllText(filePath, "v2");
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(1));
        await fixture.Tracker.CommitAsync("Nova", roomA, collectedA1, [filePath], ct);

        CollectedChanges collectedA2 = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        File.WriteAllText(filePath, "v3");
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(2));
        await fixture.Tracker.CommitAsync("Nova", roomA, collectedA2, [filePath], ct);

        CollectedChanges nextB = await fixture.Tracker.CollectAsync("Nova", roomB, [], ct);
        FileChange change = Assert.Single(nextB.Report.Changes);
        Assert.Equal(FileChangeKind.Changed, change.Kind);
    }

    /// <summary>E-3: every file under a deleted watched folder is reported once, not on every subsequent collect.</summary>
    [Fact]
    public async Task Collect_FolderDeletedAfterWatched_EachFileDeletedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string watchedDir = Path.Combine(fixture.DataDir.Path, "watched");
        Directory.CreateDirectory(watchedDir);
        File.WriteAllText(Path.Combine(watchedDir, "f1.md"), "one");
        File.WriteAllText(Path.Combine(watchedDir, "f2.md"), "two");

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, ["./watched"], ct), [], ct);

        Directory.Delete(watchedDir, recursive: true);

        CollectedChanges first = await fixture.Tracker.CollectAsync("Nova", roomA, ["./watched"], ct);
        Assert.Equal(2, first.Report.Changes.Count);
        Assert.All(first.Report.Changes, change => Assert.Equal(FileChangeKind.Deleted, change.Kind));

        await fixture.Tracker.CommitAsync("Nova", roomA, first, [], ct);

        CollectedChanges second = await fixture.Tracker.CollectAsync("Nova", roomA, ["./watched"], ct);
        Assert.True(second.Report.IsEmpty);
    }

    /// <summary>E-2: a watched folder that does not exist yet reports nothing; once created, its files are Added.</summary>
    [Fact]
    public async Task Collect_FolderMissingThenCreated_FilesAdded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string watchedDir = Path.Combine(fixture.DataDir.Path, "watched");

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, ["./watched"], ct), [], ct);

        Directory.CreateDirectory(watchedDir);
        File.WriteAllText(Path.Combine(watchedDir, "f1.md"), "one");

        CollectedChanges next = await fixture.Tracker.CollectAsync("Nova", roomA, ["./watched"], ct);
        FileChange change = Assert.Single(next.Report.Changes);
        Assert.Equal(FileChangeKind.Added, change.Kind);
    }

    /// <summary>E-4: a folder over the cap reports Unchecked and its previous snapshot is kept.</summary>
    [Fact]
    public async Task Collect_TooLargeFolder_ReportsUncheckedAndKeepsPreviousSnapshot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, options => options.MaxFilesPerFolder = 2);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string watchedDir = Path.Combine(fixture.DataDir.Path, "watched");
        Directory.CreateDirectory(watchedDir);
        File.WriteAllText(Path.Combine(watchedDir, "f1.md"), "one");

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, ["./watched"], ct), [], ct);

        File.WriteAllText(Path.Combine(watchedDir, "f2.md"), "two");
        File.WriteAllText(Path.Combine(watchedDir, "f3.md"), "three");

        CollectedChanges next = await fixture.Tracker.CollectAsync("Nova", roomA, ["./watched"], ct);
        string fullPath = Assert.Single(next.Report.Unchecked);
        Assert.Equal(watchedDir, fullPath);
        Assert.Empty(next.Report.Changes);

        await fixture.Tracker.CommitAsync("Nova", roomA, next, [], ct);

        File.Delete(Path.Combine(watchedDir, "f2.md"));
        File.Delete(Path.Combine(watchedDir, "f3.md"));

        CollectedChanges afterShrink = await fixture.Tracker.CollectAsync("Nova", roomA, ["./watched"], ct);
        Assert.True(afterShrink.Report.IsEmpty);
    }

    /// <summary>F10: more changes than <c>MaxListed</c> are capped, and the rest are counted in <c>NotListed</c>.</summary>
    [Fact]
    public async Task Collect_OverMaxListed_CapsAndCountsNotListed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, options => options.MaxListed = 3);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string workDir = WorkDir(fixture);
        Directory.CreateDirectory(workDir);
        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(workDir, $"f{i}.md"), "v1");
        }

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);

        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(workDir, $"f{i}.md"), "v2 changed");
            File.SetLastWriteTimeUtc(Path.Combine(workDir, $"f{i}.md"), DateTime.UtcNow.AddMinutes(i + 1));
        }

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        Assert.Equal(3, collected.Report.Changes.Count);
        Assert.Equal(2, collected.Report.NotListed);
    }

    /// <summary>E-1c: a commit prunes the baseline of a Room that no longer exists.</summary>
    [Fact]
    public async Task Commit_DeletedRoom_BaselinePruned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        const string ghostRoomId = "ghost-room";

        fixture.Store.Save(
            "Nova",
            new FileState(
                [],
                new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
                {
                    [roomA] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)),
                    [ghostRoomId] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)),
                },
                new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase)));

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomA, collected, [], ct);

        FileState? loaded = fixture.Store.Load("Nova");
        Assert.NotNull(loaded);
        Assert.True(loaded.Rooms.ContainsKey(roomA));
        Assert.False(loaded.Rooms.ContainsKey(ghostRoomId));
    }

    /// <summary>E-1d: two Rooms' baselines never absorb one another's own edit.</summary>
    [Fact]
    public async Task Commit_TwoRoomsOverlapping_NeitherAbsorbsTheOther()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string roomB = await CreateRoomAsync(fixture, "RoomB", ct);
        string workDir = WorkDir(fixture);

        CollectedChanges collectedA1 = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomA, collectedA1, [], ct);
        CollectedChanges collectedB1 = await fixture.Tracker.CollectAsync("Nova", roomB, [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomB, collectedB1, [], ct);

        CollectedChanges collectedA2 = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        CollectedChanges collectedB2 = await fixture.Tracker.CollectAsync("Nova", roomB, [], ct);

        Directory.CreateDirectory(workDir);
        string file1 = Path.Combine(workDir, "f1.md");
        string file2 = Path.Combine(workDir, "f2.md");
        File.WriteAllText(file1, "one");
        File.WriteAllText(file2, "two");

        await fixture.Tracker.CommitAsync("Nova", roomA, collectedA2, [file1], ct);
        await fixture.Tracker.CommitAsync("Nova", roomB, collectedB2, [file2], ct);

        CollectedChanges nextA = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        FileChange changeA = Assert.Single(nextA.Report.Changes);
        Assert.Equal(file2, changeA.FullPath);

        CollectedChanges nextB = await fixture.Tracker.CollectAsync("Nova", roomB, [], ct);
        FileChange changeB = Assert.Single(nextB.Report.Changes);
        Assert.Equal(file1, changeB.FullPath);
    }

    /// <summary>The Watched Folder list is own, then declared, then subscribed, deduplicated by resolved full path.</summary>
    [Fact]
    public async Task Collect_WatchedFolders_OwnThenDeclaredThenSubscribed_NoRepeats()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        fixture.Store.Save("Nova", new FileState(
            ["Shared/x"],
            new Dictionary<string, RoomBaseline>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase)));

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", ["Shared/x", "Nova"], ct);

        Assert.Equal(2, collected.Folders.Count);
        Assert.Equal("Nova", collected.Folders[0].Entry);
        Assert.Equal("Shared/x", collected.Folders[1].Entry);
    }

    /// <summary>An unresolvable declared entry is skipped, with the rest still scanned.</summary>
    [Fact]
    public async Task Collect_UnresolvableEntry_SkippedOthersStillScanned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", ["../outside", "Nova"], ct);

        WatchedFolder folder = Assert.Single(collected.Folders);
        Assert.Equal("Nova", folder.Entry);
    }

    /// <summary>A new subscription is saved and returns the "Now watching" text.</summary>
    [Fact]
    public async Task Subscribe_New_SavesAndReturnsNowWatching()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string expectedPath = Path.GetFullPath(Path.Combine(fixture.DataDir.Path, "Shared", "pricing"));

        string result = fixture.Tracker.Subscribe("Nova", "Shared/pricing");

        Assert.Equal(
            $"Now watching 'Shared/pricing' ({expectedPath}). From your next Turn, files added, changed or deleted there are listed at the top of your prompt. This lasts until you call unwatch_folder, including after a restart.",
            result);
        FileState? state = fixture.Store.Load("Nova");
        Assert.Contains("Shared/pricing", state!.Subscribed);
    }

    /// <summary>Subscribing again with a different spelling of an already-subscribed folder changes nothing.</summary>
    [Fact]
    public async Task Subscribe_AlreadySubscribedByOtherSpelling_ReturnsAlready()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string expectedPath = Path.GetFullPath(Path.Combine(fixture.DataDir.Path, "Shared", "pricing"));
        fixture.Tracker.Subscribe("Nova", "Shared/pricing");

        string result = fixture.Tracker.Subscribe("Nova", expectedPath);

        Assert.Equal($"Already watching 'Shared/pricing' ({expectedPath}). Nothing to do.", result);
        FileState? state = fixture.Store.Load("Nova");
        Assert.Equal(["Shared/pricing"], state!.Subscribed);
    }

    /// <summary>Subscribing to the Agent's own Work Dir is already watched.</summary>
    [Fact]
    public async Task Subscribe_OwnWorkDir_ReturnsAlready()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);

        string result = fixture.Tracker.Subscribe("Nova", "Nova");

        Assert.Equal($"Already watching 'Nova' ({WorkDir(fixture)}). Nothing to do.", result);
    }

    /// <summary>E-10: subscribing to a folder already declared in the Persona's frontmatter is already watched.</summary>
    [Fact]
    public async Task Subscribe_FrontmatterEntry_ReturnsAlready()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, watches: "Shared/pricing");
        string expectedPath = Path.GetFullPath(Path.Combine(fixture.DataDir.Path, "Shared", "pricing"));

        string result = fixture.Tracker.Subscribe("Nova", "Shared/pricing");

        Assert.Equal($"Already watching 'Shared/pricing' ({expectedPath}). Nothing to do.", result);
    }

    /// <summary>An entry the resolver refuses returns the resolver's own reason.</summary>
    [Fact]
    public async Task Subscribe_Unresolvable_ReturnsTheResolverReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);

        string result = fixture.Tracker.Subscribe("Nova", "rooms");

        Assert.Equal("'rooms' holds Huddle's own data, not working files.", result);
    }

    /// <summary>The Agent's own Work Dir cannot be unwatched.</summary>
    [Fact]
    public async Task Unsubscribe_OwnWorkDir()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);

        string result = fixture.Tracker.Unsubscribe("Nova", "Nova");

        Assert.Equal("Your own folder is always watched.", result);
    }

    /// <summary>A folder declared in frontmatter cannot be unwatched by the Agent.</summary>
    [Fact]
    public async Task Unsubscribe_Frontmatter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, watches: "Shared/pricing");

        string result = fixture.Tracker.Unsubscribe("Nova", "Shared/pricing");

        Assert.Equal("'Shared/pricing' is watched because your Persona lists it. Only the Human can change that.", result);
    }

    /// <summary>Unwatching a folder that was never subscribed says so.</summary>
    [Fact]
    public async Task Unsubscribe_NotWatched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);

        string result = fixture.Tracker.Unsubscribe("Nova", "x");

        Assert.Equal("You are not watching 'x'.", result);
    }

    /// <summary>Unwatching a subscribed folder removes it, and every Room's snapshot of it.</summary>
    [Fact]
    public async Task Unsubscribe_Subscribed_RemovesEntryAndEverySnapshotOfIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        fixture.Tracker.Subscribe("Nova", "Shared/pricing");
        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);
        FileState? beforeState = fixture.Store.Load("Nova");
        Assert.True(beforeState!.Rooms[roomA].Folders.ContainsKey("Shared/pricing"));

        string result = fixture.Tracker.Unsubscribe("Nova", "Shared/pricing");

        Assert.Equal("Stopped watching 'Shared/pricing'.", result);
        FileState? afterState = fixture.Store.Load("Nova");
        Assert.DoesNotContain("Shared/pricing", afterState!.Subscribed);
        Assert.False(afterState.Rooms[roomA].Folders.ContainsKey("Shared/pricing"));
    }

    /// <summary>
    /// An unresolvable declared entry produces the fixed warning text FC §6.10 defines, regardless
    /// of the resolver's own specific reason. Deviation: FC's own sample entry "Nope" in fact
    /// resolves under <see cref="WatchedFolderResolver"/> (a bare word with no slash is a folder
    /// relative to DataDir, per its ordered rules), so this test uses "../nope" (outside DataDir)
    /// to produce a genuinely unresolvable entry, keeping the fixed message format FC §6.10 defines.
    /// </summary>
    [Fact]
    public async Task CheckDeclared_UnresolvableEntries_ReturnWarnings()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string dataDirName = Path.GetFileName(fixture.DataDir.Path);

        IReadOnlyList<string> warnings = fixture.Tracker.CheckDeclared(["../nope"]);

        string warning = Assert.Single(warnings);
        Assert.Equal($"Watched folder '../nope' is not a Teammate or a folder inside {dataDirName}.", warning);
    }

    /// <summary>The Agent's own Work Dir, per FC §6.7 step 1: <c>{DataDir}/work/Nova</c>.</summary>
    private static string WorkDir(Fixture fixture) => Path.Combine(fixture.DataDir.Path, "work", "Nova");

    /// <summary>Creates a real Room through the fixture's <see cref="ITeamDirectory"/>, returning its id.</summary>
    private static async Task<string> CreateRoomAsync(Fixture fixture, string name, CancellationToken ct)
    {
        Room room = await fixture.Directory.CreateRoomAsync(name, [KnownIds.Human], ct);
        return room.Id;
    }

    /// <summary>
    /// Builds a fixture with a real <see cref="SqliteTeamDirectory"/>, a real <see cref="PersonaStore"/>
    /// holding a Persona "Nova" (frontmatter written before the store is constructed, matching
    /// <c>PersonaSupervisorTests.WritePersonaFile</c>), a real <see cref="FileStateStore"/>, a real
    /// <see cref="WatchedFolderResolver"/>, and the <see cref="FileChangeTracker"/> under test.
    /// </summary>
    private static async Task<Fixture> CreateFixtureAsync(
        CancellationToken ct,
        Action<FileChangesOptions>? configureFileChanges = null,
        string? watches = null)
    {
        TempDataDir dataDir = new();
        IOptions<TeamOptions> options = dataDir.Options();
        configureFileChanges?.Invoke(options.Value.FileChanges);

        string teamsDir = Path.Combine(options.Value.DataDir, options.Value.Acp.TeamsDir);
        Directory.CreateDirectory(teamsDir);
        string watchesLine = watches is null ? string.Empty : $"\nwatches: [{watches}]";
        File.WriteAllText(Path.Combine(teamsDir, "nova.md"), $"---\nName: Nova\nTitle: Nova\nAlias: Nova{watchesLine}\n---\nYou are Nova.");

        SqliteTeamDirectory directory = new(options);
        await directory.InitializeAsync("You", ct);

        PersonaStore personas = new(
            options,
            new PersonaModelStore(options),
            new PersonaEffortStore(options),
            NullLogger<PersonaStore>.Instance);
        FileStateStore store = new(options, NullLogger<FileStateStore>.Instance);
        WatchedFolderResolver resolver = new(options);
        FileChangeTracker tracker = new(store, personas, directory, resolver, options, NullLogger<FileChangeTracker>.Instance);

        return new Fixture(dataDir, directory, personas, store, tracker);
    }

    /// <summary>Bundles the real collaborators one <see cref="FileChangeTracker"/> under test needs, torn down together.</summary>
    private sealed class Fixture(TempDataDir dataDir, SqliteTeamDirectory directory, PersonaStore personas, FileStateStore store, FileChangeTracker tracker) : IDisposable
    {
        /// <summary>The isolated temp data directory backing this fixture.</summary>
        public TempDataDir DataDir { get; } = dataDir;

        /// <summary>The real <see cref="SqliteTeamDirectory"/> backing this fixture's Rooms.</summary>
        public SqliteTeamDirectory Directory { get; } = directory;

        /// <summary>The real <see cref="FileStateStore"/> backing this fixture's saved state.</summary>
        public FileStateStore Store { get; } = store;

        /// <summary>The <see cref="FileChangeTracker"/> under test.</summary>
        public FileChangeTracker Tracker { get; } = tracker;

        /// <inheritdoc/>
        public void Dispose()
        {
            personas.Dispose();
            this.DataDir.Dispose();
        }
    }
}
