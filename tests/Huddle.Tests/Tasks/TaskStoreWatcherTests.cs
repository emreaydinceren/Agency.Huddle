using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskStore"/>'s filesystem watcher: noticing an edit made outside Huddle
/// (Spec §8.4, Spec §4 principle 3), under the Settled corrections-B2 D5 rules - one lock
/// (<c>writeGate</c>) shared with <see cref="TaskStore.Write"/>/<see cref="TaskStore.Move"/>, no
/// <see cref="TaskStore.IndexChanged"/> from a rebuild that changes nothing, and a forced rebuild
/// from <see cref="TaskStore.OnWatcherError"/> that always raises it. Every test pre-creates the
/// Team folder it writes into before constructing the store (item 18): inotify can miss a file
/// written into a just-created subdirectory on Linux CI.
/// </summary>
public sealed class TaskStoreWatcherTests
{
    /// <summary>A file changed outside Huddle raises <see cref="TaskStore.OutsideEditDetected"/> exactly once, with the Task's state before and after the edit.</summary>
    [Fact]
    public async Task OutsideEdit_FileChanged_RaisedOnceWithBeforeAndAfter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", title: "original", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");

        List<OutsideEdit> raised = [];
        TaskCompletionSource outsideEditDetected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OutsideEditDetected += edit =>
        {
            raised.Add(edit);
            outsideEditDetected.TrySetResult();
        };

        await File.WriteAllTextAsync(path, TaskFileFormat.Compose(original with { Title = "changed outside" }), ct);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => outsideEditDetected.TrySetCanceled());
        await outsideEditDetected.Task;

        OutsideEdit edit = Assert.Single(raised);
        Assert.NotNull(edit.Before);
        Assert.Equal("original", edit.Before?.Title);
        Assert.Equal("changed outside", edit.After.Title);
    }

    /// <summary>A write made through <see cref="TaskStore.Write"/> itself is never reported as an outside edit, because the watcher's rebuild sees its own write's version as <c>lastSeenVersion</c> (Spec §8.4, Spec §4 principle 3). Waits 750&#160;ms, a full debounce window and then some, before asserting the negative.</summary>
    [Fact]
    public async Task OwnWrite_NotReportedAsOutsideEdit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");

        bool raised = false;
        store.OutsideEditDetected += _ => raised = true;

        TaskItem? written = store.Write(original, original.Version, TaskFileFormat.Compose(original with { Title = "via store" }));
        Assert.NotNull(written);

        await Task.Delay(TimeSpan.FromMilliseconds(750), ct);

        Assert.False(raised);
    }

    /// <summary>
    /// A <see cref="TaskStore.Create"/> into a Team folder that did not exist before must not raise a
    /// second, spurious <see cref="TaskStore.IndexChanged"/> once the real watcher's debounced rebuild
    /// notices that folder: <see cref="TaskStore.Create"/> already refreshes its own in-memory Team
    /// list, so the rebuild's freshly-scanned Teams match what <see cref="TaskStore.Create"/> already
    /// published and finds nothing to report. Before this fix, <see cref="TaskStore.Create"/> left the
    /// stale (folder-less) Team list in place, so the watcher's rebuild ~500&#160;ms later always saw a
    /// "new" Team and republished - a needless index reload that a UI component such as
    /// <c>TaskDetail</c>, subscribed to reloads, would visibly re-render for, which is what made
    /// <c>TaskDetailFieldsTests</c> flaky under load (Conversation delivery-brief D7). Waits 900&#160;ms,
    /// a full debounce window and then some, before asserting the negative.
    /// </summary>
    [Fact]
    public async Task Create_IntoBrandNewTeamFolder_DoesNotRaiseASecondIndexChangedOnceTheWatcherCatchesUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        int indexChangedCount = 0;
        store.IndexChanged += () => indexChangedCount++;

        TaskItem candidate = TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false));
        TaskItem toWrite = candidate with { Path = TaskLayout.PathFor(store.RootDirectory, candidate.Location, candidate.Id) };
        TaskItem? written = store.Create(toWrite, TaskFileFormat.Compose(toWrite));
        Assert.NotNull(written);
        int countRightAfterCreate = indexChangedCount;

        await Task.Delay(TimeSpan.FromMilliseconds(900), ct);

        Assert.Equal(1, countRightAfterCreate);
        Assert.Equal(1, indexChangedCount);
        Assert.Single(store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal));
    }

    /// <summary>
    /// Two <see cref="TaskStore.Create"/> calls into the same, already-existing Team folder (the
    /// <c>TaskDetailFieldsTests</c> setup shape: create one Task, then a second one to reference as a
    /// duplicate target) raise exactly one <see cref="TaskStore.IndexChanged"/> per call and no more,
    /// once the real watcher's debounced rebuild has had a full window to catch up.
    /// </summary>
    [Fact]
    public async Task Create_TwiceIntoTheSameFolder_RaisesExactlyOneIndexChangedPerCreate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        int indexChangedCount = 0;
        store.IndexChanged += () => indexChangedCount++;

        TaskItem c1 = TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false));
        TaskItem w1 = c1 with { Path = TaskLayout.PathFor(store.RootDirectory, c1.Location, c1.Id) };
        Assert.NotNull(store.Create(w1, TaskFileFormat.Compose(w1)));

        TaskItem c2 = TestTasks.Make(id: "PLAT-0002", title: "Other task", location: new("Platform", null, false));
        TaskItem w2 = c2 with { Path = TaskLayout.PathFor(store.RootDirectory, c2.Location, c2.Id) };
        Assert.NotNull(store.Create(w2, TaskFileFormat.Compose(w2)));

        int countRightAfter = indexChangedCount;
        await Task.Delay(TimeSpan.FromMilliseconds(900), ct);

        Assert.Equal(2, countRightAfter);
        Assert.Equal(2, indexChangedCount);
    }

    /// <summary>The same fix as <see cref="Create_IntoBrandNewTeamFolder_DoesNotRaiseASecondIndexChangedOnceTheWatcherCatchesUp"/>, for <see cref="TaskStore.Move"/>: moving a Task into a Team folder that did not exist before must not raise a second, spurious <see cref="TaskStore.IndexChanged"/> once the watcher's debounced rebuild notices that folder.</summary>
    [Fact]
    public async Task Move_IntoBrandNewTeamFolder_DoesNotRaiseASecondIndexChangedOnceTheWatcherCatchesUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");

        int indexChangedCount = 0;
        store.IndexChanged += () => indexChangedCount++;

        TaskLocation newTeamLocation = new("Legal", null, false);
        TaskItem? written = store.Move(original, original.Version, newTeamLocation, TaskFileFormat.Compose(original with { Location = newTeamLocation }));
        Assert.NotNull(written);
        int countRightAfterMove = indexChangedCount;

        await Task.Delay(TimeSpan.FromMilliseconds(900), ct);

        Assert.Equal(1, countRightAfterMove);
        Assert.Equal(1, indexChangedCount);
        Assert.Single(store.Teams, team => string.Equals(team.Name, "Legal", StringComparison.Ordinal));
    }

    /// <summary>A file that appears where none existed before raises <see cref="TaskStore.OutsideEditDetected"/> with a <see langword="null"/> <see cref="OutsideEdit.Before"/> (Spec §8.4).</summary>
    [Fact]
    public async Task OutsideEdit_NewFile_BeforeIsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        List<OutsideEdit> raised = [];
        TaskCompletionSource outsideEditDetected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OutsideEditDetected += edit =>
        {
            raised.Add(edit);
            outsideEditDetected.TrySetResult();
        };

        string path = Path.Combine(root, "Platform", "PLAT-0001.md");
        await File.WriteAllTextAsync(
            path,
            TaskFileFormat.Compose(TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false))),
            ct);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => outsideEditDetected.TrySetCanceled());
        await outsideEditDetected.Task;

        OutsideEdit edit = Assert.Single(raised);
        Assert.Null(edit.Before);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        Assert.Equal(id, edit.After.Id);
    }

    /// <summary>A Task moved to a different Team folder outside Huddle is still found by id against the previous index, and reported with its old and new Location (Spec §8.4).</summary>
    [Fact]
    public async Task OutsideEdit_FileMovedToOtherTeam_FoundById()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string sourcePath = TestTaskStore.WriteTask(root, Path.Combine("Platform", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        Directory.CreateDirectory(Path.Combine(root, "Ops"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");

        List<OutsideEdit> raised = [];
        TaskCompletionSource outsideEditDetected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OutsideEditDetected += edit =>
        {
            raised.Add(edit);
            outsideEditDetected.TrySetResult();
        };

        TaskLocation newLocation = new("Ops", null, false);
        string targetPath = Path.Combine(root, "Ops", "PLAT-0001.md");
        await File.WriteAllTextAsync(targetPath, TaskFileFormat.Compose(original with { Location = newLocation }), ct);
        File.Delete(sourcePath);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => outsideEditDetected.TrySetCanceled());
        await outsideEditDetected.Task;

        OutsideEdit edit = Assert.Single(raised);
        Assert.NotNull(edit.Before);
        Assert.Equal("Platform", edit.Before?.Location.Team);
        Assert.Equal("Ops", edit.After.Location.Team);
    }

    /// <summary>A file deleted outside Huddle leaves the index silently: it disappears from <see cref="TaskStore.All"/>, <see cref="TaskStore.IndexChanged"/> still fires for the rebuild that removed it, but no <see cref="TaskStore.OutsideEditDetected"/> is raised - there is no file left to log into (Spec §8.4, Spec E-9).</summary>
    [Fact]
    public async Task FileDeleted_RemovedFromIndex_NoOutsideEdit()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        Assert.NotNull(store.Get(id));

        bool outsideEditRaised = false;
        store.OutsideEditDetected += _ => outsideEditRaised = true;
        TaskCompletionSource indexChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.IndexChanged += () => indexChanged.TrySetResult();

        File.Delete(path);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => indexChanged.TrySetCanceled());
        await indexChanged.Task;

        Assert.Null(store.Get(id));
        Assert.False(outsideEditRaised);
    }

    /// <summary>A ".md.tmp" file - this class's own atomic-write artefact - is ignored by the watcher's handler, never indexed and never reported as an outside edit (Spec §8.4). Waits 750&#160;ms, a full debounce window and then some, before asserting the negative.</summary>
    [Fact]
    public async Task TmpFile_Ignored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        bool outsideEditRaised = false;
        store.OutsideEditDetected += _ => outsideEditRaised = true;
        bool indexChangedRaised = false;
        store.IndexChanged += () => indexChangedRaised = true;

        await File.WriteAllTextAsync(Path.Combine(root, "Platform", "PLAT-0001.md.tmp"), "not a real task", ct);
        await Task.Delay(TimeSpan.FromMilliseconds(750), ct);

        Assert.False(outsideEditRaised);
        Assert.False(indexChangedRaised);
        Assert.Empty(store.All);
    }

    /// <summary>
    /// The folder's own Created event is the one signal a new Team or Project sub-folder is
    /// guaranteed to raise on every platform, so it alone must be recognised as affecting a Task
    /// file: on Linux the inotify watch for a new sub-folder is only added after that folder's
    /// Created event is read, so a Task file written straight into it in that gap raises nothing at
    /// all, and only the folder's own Created event survives to schedule the rescan that finds it
    /// from disk (traps.md, "On Linux, IncludeSubdirectories = true still misses a file written into
    /// a sub-folder that was just created"). No integration test exercises this end-to-end for
    /// <see cref="TaskStore"/> - unlike <c>PersonaStoreTests</c>'s
    /// <c>ExternalFileCreated_InANewlyCreatedTeamSubFolder_IsNoticedThroughPersonasChanged</c>, every
    /// other test in this class deliberately pre-creates the Team folder before constructing the
    /// store (see the class summary, item 18) specifically to avoid this gap, so this unit test is
    /// the only coverage of the clause.
    /// </summary>
    [Fact]
    public void AffectsATaskFile_ANewSubFolder_SchedulesARescan()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        FileSystemEventArgs created = new(WatcherChangeTypes.Created, root, "Platform");

        Assert.True(TaskStore.AffectsATaskFile(created));
    }

    /// <summary>Calling the internal <see cref="TaskStore.OnWatcherError"/> directly - as the real <see cref="System.IO.FileSystemWatcher.Error"/> event does on a dropped-event buffer overflow - schedules a full rebuild that raises <see cref="TaskStore.IndexChanged"/>, even though nothing on disk actually changed (Settled corrections-B2 D5 item 7).</summary>
    [Fact]
    public async Task WatcherError_TriggersFullRebuild()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);

        TaskCompletionSource indexChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.IndexChanged += () => indexChanged.TrySetResult();

        store.OnWatcherError(store, new ErrorEventArgs(new IOException("simulated buffer overflow")));

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => indexChanged.TrySetCanceled());
        await indexChanged.Task;

        Assert.NotNull(store.Get(id));
    }

    /// <summary>
    /// After the watcher's rebuild picks up an outside edit, the new version is recorded in
    /// <see cref="TaskStore.GetVersion"/>'s history exactly as a <see cref="TaskStore.Write"/> would
    /// record it - version history is not only a Write/Move side effect (Settled corrections-B2 D5
    /// item 8). D6's evicted-base conflict path depends on this for a Task nobody ever wrote through
    /// the store.
    /// </summary>
    [Fact]
    public async Task OutsideEdit_RebuildRecordsTheNewVersion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", title: "original", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");

        TaskCompletionSource outsideEditDetected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OutsideEditDetected += _ => outsideEditDetected.TrySetResult();

        await File.WriteAllTextAsync(path, TaskFileFormat.Compose(original with { Title = "changed outside" }), ct);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => outsideEditDetected.TrySetCanceled());
        await outsideEditDetected.Task;

        TaskItem afterEdit = store.Get(id) ?? throw new InvalidOperationException("task vanished from the index");
        Assert.NotEqual(original.Version, afterEdit.Version);

        TaskItem? recorded = store.GetVersion(id, afterEdit.Version);
        Assert.NotNull(recorded);
        Assert.Equal("changed outside", recorded.Title);
    }
}
