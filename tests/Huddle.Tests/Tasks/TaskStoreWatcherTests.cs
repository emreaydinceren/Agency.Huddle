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
