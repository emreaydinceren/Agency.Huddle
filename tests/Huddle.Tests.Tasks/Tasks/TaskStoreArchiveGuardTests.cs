using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins corrections-12 item 5: <see cref="TaskStore"/> never re-creates a Team folder (so an archive move cannot
/// be undone by a late Task write), and <see cref="TaskStore.PauseWrites"/> refuses every write to one Team while an
/// archive move holds the scope.
/// </summary>
public sealed class TaskStoreArchiveGuardTests
{
    /// <summary>
    /// A Task for a Team the store has indexed whose <c>Teams/&lt;T&gt;</c> folder was taken away (an archive move) is
    /// refused and no folder appears. A Team the store never saw is not refused: its first Task creates the folder.
    /// </summary>
    [Fact]
    public void Create_IndexedTeamFolderMissing_RefusedAndNoFolderCreated()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Home", "_tasks", "HOME-0001.md"), TestTasks.Make(id: "HOME-0001", location: new("Home", null, false)));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        Directory.Delete(Path.Combine(root, "Home"), recursive: true);
        TaskItem candidate = TestTasks.Make(id: "HOME-0002", location: new("Home", null, false), path: "");

        _ = Assert.Throws<InvalidOperationException>(() => store.Create(candidate, TaskFileFormat.Compose(candidate)));

        Assert.False(Directory.Exists(Path.Combine(root, "Home")));
    }

    /// <summary>A Create into a Project that sits in <c>Archive/Home/Alpha</c> (and not in <c>Teams/Home</c>) is refused and no live Project folder appears.</summary>
    [Fact]
    public void Create_ArchivedProject_RefusedAndNoFolderCreated()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Home"));
        Directory.CreateDirectory(Path.Combine(dir.Path, "Archive", "Home", "Alpha"));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskItem candidate = TestTasks.Make(id: "HOME-0002", location: new("Home", "Alpha", false), path: "");

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(() => store.Create(candidate, TaskFileFormat.Compose(candidate)));

        Assert.Equal("Tasks of Home/Alpha can't be written: the Project is archived. Unarchive it to add Tasks.", refusal.Message);
        Assert.False(Directory.Exists(Path.Combine(root, "Home", "Alpha")));
    }

    /// <summary>A Move into an archived Project is refused, the Task stays where it was and no live Project folder appears.</summary>
    [Fact]
    public void Move_IntoArchivedProject_RefusedAndTaskStays()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Home", "_tasks", "HOME-0001.md"), TestTasks.Make(id: "HOME-0001", location: new("Home", null, false)));
        Directory.CreateDirectory(Path.Combine(dir.Path, "Archive", "Home", "Alpha"));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("HOME-0001", out TaskId id);
        TaskItem current = Assert.IsType<TaskItem>(store.Get(id));
        string version = TaskFileFormat.Compose(current);

        _ = Assert.Throws<InvalidOperationException>(() => store.Move(current, version, new("Home", "Alpha", false), version));

        Assert.True(File.Exists(current.Path));
        Assert.False(Directory.Exists(Path.Combine(root, "Home", "Alpha")));
    }

    /// <summary>A batch write for a Task whose Project was archived after it was indexed is refused and no live Project folder is re-created.</summary>
    [Fact]
    public void WriteMany_ArchivedProject_RefusedAndNoFolderCreated()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Home", "Alpha", "_tasks", "HOME-0001.md"), TestTasks.Make(id: "HOME-0001", title: "v0", location: new("Home", "Alpha", false)));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("HOME-0001", out TaskId id);
        TaskItem current = Assert.IsType<TaskItem>(store.Get(id));
        string version = TaskFileFormat.Compose(current);
        Directory.CreateDirectory(Path.Combine(dir.Path, "Archive", "Home"));
        Directory.Move(Path.Combine(root, "Home", "Alpha"), Path.Combine(dir.Path, "Archive", "Home", "Alpha"));

        _ = Assert.Throws<InvalidOperationException>(() => store.WriteMany([(current, version, version.Replace("v0", "v1", StringComparison.Ordinal))]));

        Assert.False(Directory.Exists(Path.Combine(root, "Home", "Alpha")));
    }

    /// <summary>A Project that is live in <c>Teams/Home</c> and also archived under the same name is not refused: only a Project that exists in the archive alone is archived.</summary>
    [Fact]
    public void Create_ProjectLiveAndArchived_Succeeds()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Home", "Alpha"));
        Directory.CreateDirectory(Path.Combine(dir.Path, "Archive", "Home", "Alpha"));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskItem candidate = TestTasks.Make(id: "HOME-0001", location: new("Home", "Alpha", false), path: "");

        TaskItem? written = store.Create(candidate, TaskFileFormat.Compose(candidate));

        Assert.NotNull(written);
    }

    /// <summary>While a Team's writes are paused a Create for it is refused; after the scope is disposed the same Create succeeds.</summary>
    [Fact]
    public void Create_WhilePaused_RefusedThenWorksAfterDispose()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Home"));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskItem candidate = TestTasks.Make(id: "HOME-0001", location: new("Home", null, false), path: "");
        string text = TaskFileFormat.Compose(candidate);

        IDisposable scope = store.PauseWrites("home");
        _ = Assert.Throws<InvalidOperationException>(() => store.Create(candidate, text));
        scope.Dispose();
        TaskItem? written = store.Create(candidate, text);

        // The pause names "home"; the Team folder is "Home": the match ignores case.
        Assert.NotNull(written);
    }

    /// <summary>A pause on one Team leaves another Team's Tasks writable.</summary>
    [Fact]
    public void Create_OtherTeamWhilePaused_Succeeds()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Home"));
        Directory.CreateDirectory(Path.Combine(root, "Other"));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskItem candidate = TestTasks.Make(id: "OTHR-0001", location: new("Other", null, false), path: "");

        using IDisposable scope = store.PauseWrites("Home");
        TaskItem? written = store.Create(candidate, TaskFileFormat.Compose(candidate));

        Assert.NotNull(written);
    }

    /// <summary>A batch write that touches a paused Team is refused as a whole and the file keeps its bytes.</summary>
    [Fact]
    public void WriteMany_WhilePaused_RefusedAndFileUntouched()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Home", "_tasks", "HOME-0001.md"), TestTasks.Make(id: "HOME-0001", title: "v0", location: new("Home", null, false)));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("HOME-0001", out TaskId id);
        TaskItem current = Assert.IsType<TaskItem>(store.Get(id));
        string before = File.ReadAllText(current.Path);
        string version = TaskFileFormat.Compose(current);

        using IDisposable scope = store.PauseWrites("Home");
        _ = Assert.Throws<InvalidOperationException>(() => store.WriteMany([(current, version, version.Replace("v0", "v1", StringComparison.Ordinal))]));

        Assert.Equal(before, File.ReadAllText(current.Path));
    }

    /// <summary>A batch write for an indexed Team whose folder is gone is refused.</summary>
    [Fact]
    public void WriteMany_IndexedTeamFolderMissing_Refused()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Home", "_tasks", "HOME-0001.md"), TestTasks.Make(id: "HOME-0001", title: "v0", location: new("Home", null, false)));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("HOME-0001", out TaskId id);
        TaskItem current = Assert.IsType<TaskItem>(store.Get(id));
        string version = TaskFileFormat.Compose(current);
        Directory.Delete(Path.Combine(root, "Home"), recursive: true);

        _ = Assert.Throws<InvalidOperationException>(() => store.WriteMany([(current, version, version.Replace("v0", "v1", StringComparison.Ordinal))]));

        Assert.False(Directory.Exists(Path.Combine(root, "Home")));
    }

    /// <summary>An update of an existing Task of a paused Team is refused and the file keeps its bytes.</summary>
    [Fact]
    public void Write_WhilePaused_RefusedAndFileUntouched()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Home", "_tasks", "HOME-0001.md"), TestTasks.Make(id: "HOME-0001", title: "v0", location: new("Home", null, false)));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("HOME-0001", out TaskId id);
        TaskItem current = Assert.IsType<TaskItem>(store.Get(id));
        string before = File.ReadAllText(current.Path);
        string version = TaskFileFormat.Compose(current);

        using IDisposable scope = store.PauseWrites("Home");
        _ = Assert.Throws<InvalidOperationException>(() => store.Write(current, "any", version.Replace("v0", "v1", StringComparison.Ordinal)));

        Assert.Equal(before, File.ReadAllText(current.Path));
    }

    /// <summary>
    /// A Team's Tasks moved to the Archive and back under <see cref="TaskStore.PauseWrites"/> are not outside edits, whether
    /// the watcher rebuilds during the move or after it. Unarchive used to post "edited outside Huddle" to the manager, who
    /// spent a Turn on it, and the Feed showed the Human creating the Task again.
    /// </summary>
    /// <param name="rebuildDuringMove">Whether the watcher rebuilds while the pause is still held.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArchiveAndUnarchiveMove_RaisesNoOutsideEdit(bool rebuildDuringMove)
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Home", "_tasks", "HOME-0001.md"), TestTasks.Make(id: "HOME-0001", location: new("Home", null, false)));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        List<OutsideEdit> edits = [];
        store.OutsideEditDetected += edits.Add;
        string live = Path.Combine(root, "Home");
        string archived = Path.Combine(dir.Path, "Archive", "Home");
        Directory.CreateDirectory(Path.Combine(dir.Path, "Archive"));

        MoveUnderPause(store, live, archived, rebuildDuringMove);
        store.RebuildFromWatcher();
        MoveUnderPause(store, archived, live, rebuildDuringMove);
        store.RebuildFromWatcher();

        Assert.Empty(edits);
        Assert.NotNull(store.Get(new TaskId("HOME", 1)));
    }

    /// <summary>Moves a Team folder the way an archive move does: under the Team's write pause.</summary>
    /// <param name="store">The Task store.</param>
    /// <param name="from">The folder to move.</param>
    /// <param name="to">Where it goes.</param>
    /// <param name="rebuildDuringMove">Whether the watcher rebuilds before the pause is lifted.</param>
    private static void MoveUnderPause(TaskStore store, string from, string to, bool rebuildDuringMove)
    {
        using IDisposable pause = store.PauseWrites("Home");
        Directory.Move(from, to);
        if (rebuildDuringMove)
        {
            store.RebuildFromWatcher();
        }
    }
}
