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
}
