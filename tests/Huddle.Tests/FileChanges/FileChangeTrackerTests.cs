using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.Tests.Acp;

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

    /// <summary>A touched write is recorded in <see cref="FileState.Writers"/>, keyed by entry then relative path (FC §6.15, D13).</summary>
    [Fact]
    public async Task Commit_TouchedWrite_RecordsWriter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string memoryDir = Path.Combine(WorkDir(fixture), "memory");
        Directory.CreateDirectory(memoryDir);
        string filePath = Path.Combine(memoryDir, "code-language.md");
        File.WriteAllText(filePath, "The Human prefers C#.");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomA, collected, [filePath], ct);

        FileState? loaded = fixture.Store.Load("Nova");
        Assert.NotNull(loaded);
        IReadOnlyDictionary<string, FileWriter> byPath = Assert.Contains("Nova", loaded.Writers);
        FileWriter writer = Assert.Contains(Path.Combine("memory", "code-language.md"), byPath);
        Assert.Equal(roomA, writer.RoomId);
    }

    /// <summary>
    /// A file this Agent last wrote in Room A, still exactly as that write left it, is shown in Room B
    /// with the <c>by you</c> suffix, naming Room A's CURRENT name even after a rename (FC §6.15, D13).
    /// </summary>
    [Fact]
    public async Task Collect_OwnWriteFromRoomA_InRoomB_HasByYouWithACurrentName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string roomB = await CreateRoomAsync(fixture, "RoomB", ct);

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomB, await fixture.Tracker.CollectAsync("Nova", roomB, [], ct), [], ct);

        string memoryDir = Path.Combine(WorkDir(fixture), "memory");
        Directory.CreateDirectory(memoryDir);
        string filePath = Path.Combine(memoryDir, "launch-date.md");
        File.WriteAllText(filePath, "The launch date is 2026-10-01.");

        CollectedChanges collectedA = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomA, collectedA, [filePath], ct);

        await fixture.Directory.RenameRoomAsync(roomA, "Alpha", ct);

        CollectedChanges nextB = await fixture.Tracker.CollectAsync("Nova", roomB, [], ct);
        FileChange change = Assert.Single(nextB.Report.Changes);
        Assert.Equal("Alpha", change.ByYouRoomName);
    }

    /// <summary>E-19: once someone else changes the file, the recorded write no longer matches, and the next <c>by you</c> line carries no suffix.</summary>
    [Fact]
    public async Task Collect_ChangedAfterOwnWrite_NoSuffix()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string roomB = await CreateRoomAsync(fixture, "RoomB", ct);

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomB, await fixture.Tracker.CollectAsync("Nova", roomB, [], ct), [], ct);

        string memoryDir = Path.Combine(WorkDir(fixture), "memory");
        Directory.CreateDirectory(memoryDir);
        string filePath = Path.Combine(memoryDir, "launch-date.md");
        File.WriteAllText(filePath, "The launch date is 2026-10-01.");

        CollectedChanges collectedA = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomA, collectedA, [filePath], ct);

        // The Human edits the file afterwards - a different size AND a later time, so the recorded
        // writer's FileEntry no longer equals the file's current state (correction item 19).
        File.WriteAllText(filePath, "The launch date moved to 2026-11-15, per the Human.");
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));

        CollectedChanges nextB = await fixture.Tracker.CollectAsync("Nova", roomB, [], ct);
        FileChange change = Assert.Single(nextB.Report.Changes);
        Assert.Null(change.ByYouRoomName);
    }

    /// <summary>E-20: once the writer's Room is deleted, the next <c>by you</c> line carries no suffix.</summary>
    [Fact]
    public async Task Collect_WriterRoomDeleted_NoSuffix()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string roomB = await CreateRoomAsync(fixture, "RoomB", ct);

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomB, await fixture.Tracker.CollectAsync("Nova", roomB, [], ct), [], ct);

        string memoryDir = Path.Combine(WorkDir(fixture), "memory");
        Directory.CreateDirectory(memoryDir);
        string filePath = Path.Combine(memoryDir, "launch-date.md");
        File.WriteAllText(filePath, "The launch date is 2026-10-01.");

        CollectedChanges collectedA = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);
        await fixture.Tracker.CommitAsync("Nova", roomA, collectedA, [filePath], ct);

        await fixture.Directory.DeleteRoomAsync(roomA, ct);

        CollectedChanges nextB = await fixture.Tracker.CollectAsync("Nova", roomB, [], ct);
        FileChange change = Assert.Single(nextB.Report.Changes);
        Assert.Null(change.ByYouRoomName);
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

    /// <summary>Spec §6.13: a Persona on Team "Marketing" implicitly watches its Team folder, entry <c>team:Marketing</c>.</summary>
    [Fact]
    public async Task Collect_PersonaInTeam_WatchesTeamFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "Marketing");
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string teamDir = Path.Combine(fixture.DataDir.Path, "Teams", "Marketing");
        Directory.CreateDirectory(teamDir);

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);

        string briefPath = Path.Combine(teamDir, "brief.md");
        File.WriteAllText(briefPath, "brief");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        WatchedFolder teamFolder = Assert.Single(collected.Folders, folder => folder.Entry == "team:Marketing");
        Assert.Equal(Path.GetFullPath(teamDir), teamFolder.FullPath);
        FileChange change = Assert.Single(collected.Report.Changes);
        Assert.Equal(FileChangeKind.Added, change.Kind);
        Assert.Equal(briefPath, change.FullPath);
    }

    /// <summary>
    /// Spec §6.13: files under a Team's <c>_tasks</c> or <c>_drafts</c> folder never appear, but a
    /// project folder (no underscore prefix) still does.
    /// </summary>
    [Fact]
    public async Task Collect_TaskFileUnderTeam_NotListed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "Marketing");
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string teamDir = Path.Combine(fixture.DataDir.Path, "Teams", "Marketing");
        Directory.CreateDirectory(Path.Combine(teamDir, "_tasks"));
        Directory.CreateDirectory(Path.Combine(teamDir, "_drafts"));
        Directory.CreateDirectory(Path.Combine(teamDir, "Launch Q4"));

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);

        File.WriteAllText(Path.Combine(teamDir, "_tasks", "MKT-0001.md"), "task");
        File.WriteAllText(Path.Combine(teamDir, "_drafts", "x.md"), "draft");
        string briefPath = Path.Combine(teamDir, "Launch Q4", "brief.md");
        File.WriteAllText(briefPath, "brief");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        FileChange change = Assert.Single(collected.Report.Changes);
        Assert.Equal(briefPath, change.FullPath);
    }

    /// <summary>
    /// Spec §6.4 and §12 T12: a Persona in Team "Business" is told about a new file in the Team's
    /// <c>memory</c> folder on its next Turn, because the implicit Team watch does not prune it.
    /// </summary>
    [Fact]
    public async Task Collect_NewFileInTeamMemory_ListedAsAdded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "Business");
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string memoryDir = Path.Combine(fixture.DataDir.Path, "Teams", "Business", "memory");
        Directory.CreateDirectory(memoryDir);
        Directory.CreateDirectory(Path.Combine(fixture.DataDir.Path, "Teams", "Business", "Marketing"));

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);

        string filePath = Path.Combine(memoryDir, "a.md");
        File.WriteAllText(filePath, "team fact");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        Assert.Equal(
            [(FileChangeKind.Added, filePath)],
            collected.Report.Changes.Select(change => (change.Kind, change.FullPath)));
    }

    /// <summary>
    /// Spec §6.4 and §12 T12: a new file in a Project's <c>memory</c> folder under the Team
    /// ("Business", Project "Marketing") is listed as added on the member's next Turn.
    /// </summary>
    [Fact]
    public async Task Collect_NewFileInProjectMemory_ListedAsAdded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "Business");
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string memoryDir = Path.Combine(fixture.DataDir.Path, "Teams", "Business", "Marketing", "memory");
        Directory.CreateDirectory(memoryDir);

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);

        string filePath = Path.Combine(memoryDir, "b.md");
        File.WriteAllText(filePath, "project fact");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        Assert.Equal(
            [(FileChangeKind.Added, filePath)],
            collected.Report.Changes.Select(change => (change.Kind, change.FullPath)));
    }

    /// <summary>
    /// Contrast to the <c>memory</c> rows: a new file in a Project's <c>_tasks</c> folder is pruned
    /// (underscore prefix) and never listed, while the sibling <c>memory</c> file in the same Turn is.
    /// </summary>
    [Fact]
    public async Task Collect_NewFileInProjectTasksFolder_NotListed_WhileMemorySiblingIs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "Business");
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string projectDir = Path.Combine(fixture.DataDir.Path, "Teams", "Business", "Marketing");
        Directory.CreateDirectory(Path.Combine(projectDir, "_tasks"));
        Directory.CreateDirectory(Path.Combine(projectDir, "memory"));

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);

        File.WriteAllText(Path.Combine(projectDir, "_tasks", "T.md"), "task");
        string memoryPath = Path.Combine(projectDir, "memory", "b.md");
        File.WriteAllText(memoryPath, "project fact");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        Assert.Equal(
            [(FileChangeKind.Added, memoryPath)],
            collected.Report.Changes.Select(change => (change.Kind, change.FullPath)));
    }

    /// <summary>
    /// A Persona in Team "Other" (not "Business") is told about neither the Team memory file nor the
    /// Project memory file under <c>Teams/Business</c>, even though its own Team folder is watched.
    /// </summary>
    [Fact]
    public async Task Collect_MemoryOfAnotherTeam_NotListed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "Other");
        string roomA = await CreateRoomAsync(fixture, "RoomA", ct);
        string businessDir = Path.Combine(fixture.DataDir.Path, "Teams", "Business");
        Directory.CreateDirectory(Path.Combine(fixture.DataDir.Path, "Teams", "Other"));
        Directory.CreateDirectory(Path.Combine(businessDir, "memory"));
        Directory.CreateDirectory(Path.Combine(businessDir, "Marketing", "memory"));

        await fixture.Tracker.CommitAsync("Nova", roomA, await fixture.Tracker.CollectAsync("Nova", roomA, [], ct), [], ct);

        File.WriteAllText(Path.Combine(businessDir, "memory", "a.md"), "team fact");
        File.WriteAllText(Path.Combine(businessDir, "Marketing", "memory", "b.md"), "project fact");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", roomA, [], ct);

        Assert.Equal(["Nova", "team:Other"], collected.Folders.Select(folder => folder.Entry));
        Assert.Empty(collected.Report.Changes);
    }

    /// <summary>A Persona on two Teams implicitly watches both Team folders.</summary>
    [Fact]
    public async Task Collect_TwoTeams_WatchesBoth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "Marketing, Sales");
        Directory.CreateDirectory(Path.Combine(fixture.DataDir.Path, "Teams", "Marketing"));
        Directory.CreateDirectory(Path.Combine(fixture.DataDir.Path, "Teams", "Sales"));

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", [], ct);

        Assert.Equal(
            ["Nova", "team:Marketing", "team:Sales"],
            collected.Folders.Select(folder => folder.Entry));
    }

    /// <summary>A Persona with no Team label gets no implicit Team folder.</summary>
    [Fact]
    public async Task Collect_NoTeam_NoTeamFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", [], ct);

        Assert.DoesNotContain(collected.Folders, folder => folder.Entry.StartsWith("team:", StringComparison.Ordinal));
    }

    /// <summary>A Team label whose folder doesn't exist on disk is skipped without error.</summary>
    [Fact]
    public async Task Collect_TeamFolderMissing_NoError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "Marketing");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", [], ct);

        Assert.DoesNotContain(collected.Folders, folder => folder.Entry == "team:Marketing");
    }

    /// <summary>
    /// Corrections-B4 item 38: a Persona whose declared Watched Folders also lists the Team folder
    /// watches it once, under the <c>team:</c> entry, because the implicit Team folder is inserted
    /// before declared entries and dedupe keeps the first.
    /// </summary>
    [Fact]
    public async Task Collect_DeclaredTeamFolderAndTeamLabel_WatchedOnceUnderTeamEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "Marketing");
        string teamDir = Path.Combine(fixture.DataDir.Path, "Teams", "Marketing");
        Directory.CreateDirectory(teamDir);

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", ["Teams/Marketing"], ct);

        Assert.Equal(2, collected.Folders.Count);
        Assert.Equal("Nova", collected.Folders[0].Entry);
        Assert.Equal("team:Marketing", collected.Folders[1].Entry);
        Assert.Equal(Path.GetFullPath(teamDir), collected.Folders[1].FullPath);
    }

    /// <summary>
    /// Corrections-B4 item 39: <c>PruneUnderscore</c> applies to any Watched Folder inside the Teams
    /// root, so a declared (not implicit) <c>Teams/…</c> entry still hides its <c>_tasks</c> folder,
    /// even without the matching Team label.
    /// </summary>
    [Fact]
    public async Task Collect_DeclaredTeamsFolderWithoutLabel_StillPrunesUnderscore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string teamDir = Path.Combine(fixture.DataDir.Path, "Teams", "Marketing");
        Directory.CreateDirectory(teamDir);

        await fixture.Tracker.CommitAsync(
            "Nova", "some-room", await fixture.Tracker.CollectAsync("Nova", "some-room", ["Teams/Marketing"], ct), [], ct);

        Directory.CreateDirectory(Path.Combine(teamDir, "_tasks"));
        File.WriteAllText(Path.Combine(teamDir, "_tasks", "MKT-0001.md"), "task");
        string briefPath = Path.Combine(teamDir, "brief.md");
        File.WriteAllText(briefPath, "brief");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", ["Teams/Marketing"], ct);

        FileChange change = Assert.Single(collected.Report.Changes);
        Assert.Equal(briefPath, change.FullPath);
    }

    /// <summary>A declared folder outside the Teams root keeps its own <c>_</c>-prefixed neighbour: pruning is Teams-root-only.</summary>
    [Fact]
    public async Task Collect_DeclaredFolderOutsideTeams_KeepsUnderscoreNeighbour()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string sharedDir = Path.Combine(fixture.DataDir.Path, "Shared");
        Directory.CreateDirectory(sharedDir);

        await fixture.Tracker.CommitAsync(
            "Nova", "some-room", await fixture.Tracker.CollectAsync("Nova", "some-room", ["Shared"], ct), [], ct);

        Directory.CreateDirectory(Path.Combine(sharedDir, "_notes"));
        string notePath = Path.Combine(sharedDir, "_notes", "note.md");
        File.WriteAllText(notePath, "note");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", ["Shared"], ct);

        FileChange change = Assert.Single(collected.Report.Changes);
        Assert.Equal(notePath, change.FullPath);
    }

    /// <summary>
    /// A baseline saved before <c>PruneUnderscore</c> existed (still holding a <c>_tasks</c> file
    /// entry for a Teams-root folder) must not report that file as deleted once pruning applies:
    /// the committed snapshot is filtered through the same rule the live scan uses.
    /// </summary>
    [Fact]
    public async Task Collect_PreexistingBaselineWithTaskFile_NotReportedDeleted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string teamDir = Path.Combine(fixture.DataDir.Path, "Teams", "Marketing");
        Directory.CreateDirectory(teamDir);
        File.WriteAllText(Path.Combine(teamDir, "brief.md"), "brief");

        Dictionary<string, FileEntry> preexistingFiles = new(FolderSnapshot.PathComparer)
        {
            ["brief.md"] = new FileEntry(5, DateTimeOffset.UtcNow),
            [Path.Combine("_tasks", "MKT-0001.md")] = new FileEntry(4, DateTimeOffset.UtcNow),
        };
        Dictionary<string, RoomBaseline> rooms = new(StringComparer.Ordinal)
        {
            ["some-room"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["Teams/Marketing"] = new FolderSnapshot(preexistingFiles),
            }),
        };
        fixture.Store.Save("Nova", new FileState([], rooms, new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase)));

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", ["Teams/Marketing"], ct);

        Assert.DoesNotContain(collected.Report.Changes, change => change.Kind == FileChangeKind.Deleted);
    }

    /// <summary>
    /// Corrections-B4 item 40: a Team label the Library's name validation refuses (here, <c>..</c>)
    /// is skipped with no error, and nothing outside <c>Teams/</c> gets watched because of it.
    /// </summary>
    [Fact]
    public async Task Collect_InvalidTeamLabel_SkippedNoError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct, teams: "..");

        CollectedChanges collected = await fixture.Tracker.CollectAsync("Nova", "some-room", [], ct);

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
    /// of the resolver's own specific reason - proven by "../nope" (outside DataDir, refused by
    /// <see cref="WatchedFolderResolver"/>). A bare word that resolves cleanly but names no Teammate
    /// and no existing folder ("Nope") also warns, per the delivery manager's settled correction:
    /// FC §6.10's own example is exactly this case, and D6 shipped it silently un-warned. A
    /// multi-segment entry that does not yet exist ("Shared/missing") and a bare word that resolves
    /// to a folder that already exists ("Docs") both warn on neither: FC E-2 lets a Watched Folder
    /// be created later.
    /// </summary>
    [Fact]
    public async Task CheckDeclared_UnresolvableEntries_ReturnWarnings()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await CreateFixtureAsync(ct);
        string dataDirName = Path.GetFileName(fixture.DataDir.Path);
        Directory.CreateDirectory(Path.Combine(fixture.DataDir.Path, "Docs"));
        Directory.CreateDirectory(Path.Combine(fixture.DataDir.Path, "Shared"));

        IReadOnlyList<string> warnings = fixture.Tracker.CheckDeclared(["Nope", "../nope", "Shared/missing", "Docs"]);

        Assert.Equal(2, warnings.Count);
        Assert.Contains($"Watched folder 'Nope' is not a Teammate or a folder inside {dataDirName}.", warnings);
        Assert.Contains($"Watched folder '../nope' is not a Teammate or a folder inside {dataDirName}.", warnings);
    }

    /// <summary>The Agent's own Work Dir, per FC §6.7 step 1: <c>{DataDir}/Teammates/Nova/work</c>.</summary>
    private static string WorkDir(Fixture fixture) => Path.Combine(fixture.DataDir.Path, "Teammates", "Nova", "work");

    /// <summary>Creates a real Room through the fixture's <see cref="ITeamDirectory"/>, returning its id.</summary>
    private static async Task<string> CreateRoomAsync(Fixture fixture, string name, CancellationToken ct)
    {
        Room room = await fixture.Directory.CreateRoomAsync(name, [KnownIds.Human], ct);
        return room.Id;
    }

    /// <summary>
    /// Builds a fixture with a real <see cref="SqliteTeamDirectory"/>, a real <see cref="PersonaStore"/>
    /// holding a Persona "Nova" (frontmatter written before the store is constructed, matching
    /// <c>PersonaSupervisorTestSupport.WritePersonaFile</c>), a real <see cref="FileStateStore"/>, a real
    /// <see cref="WatchedFolderResolver"/>, and the <see cref="FileChangeTracker"/> under test.
    /// </summary>
    private static async Task<Fixture> CreateFixtureAsync(
        CancellationToken ct,
        Action<FileChangesOptions>? configureFileChanges = null,
        string? watches = null,
        string? teams = null)
    {
        TempDataDir dataDir = new();
        IOptions<TeamOptions> options = dataDir.Options();
        configureFileChanges?.Invoke(options.Value.FileChanges);

        TeammatePaths teammatePaths = new(options);
        string watchesLine = watches is null ? string.Empty : $"\nwatches: [{watches}]";
        string teamsLine = teams is null ? string.Empty : $"\nTeams: [{teams}]";
        TestPersonaFiles.Write(teammatePaths, "nova", $"---\nName: Nova\nTitle: Nova\nAlias: Nova{teamsLine}{watchesLine}\n---\nYou are Nova.");

        SqliteTeamDirectory directory = new(options);
        await directory.InitializeAsync("You", ct);

        PersonaStore personas = new(
            teammatePaths,
            new PersonaModelStore(options),
            new PersonaEffortStore(options),
            NullLogger<PersonaStore>.Instance);
        FileStateStore store = new(options, NullLogger<FileStateStore>.Instance);
        WatchedFolderResolver resolver = new(options, teammatePaths);
        FileChangeTracker tracker = new(store, personas, directory, resolver, options, teammatePaths, NullLogger<FileChangeTracker>.Instance);

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
