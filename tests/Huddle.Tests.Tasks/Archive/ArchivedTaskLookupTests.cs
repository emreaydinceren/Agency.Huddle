using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins Spec 8.5's Task-id rule: an id from an archived Team still resolves for reading from its file under
/// <c>Archive/</c>, a write to it is refused, an unknown id still misses, and after unarchive it is a live Task.
/// </summary>
public sealed class ArchivedTaskLookupTests
{
    private static readonly string[] ArchivedIdSegments = ["Archive", "Home", "_tasks", "HOME-0001.md"];

    private static TaskId Id(string text)
    {
        Assert.True(TaskId.TryParse(text, out TaskId id));
        return id;
    }

    /// <summary>An id whose file sits under <c>Archive/Home/_tasks/</c> resolves to that file and is not a live Task.</summary>
    [Fact]
    public void FindArchived_IdInArchive_ResolvesToArchiveFile()
    {
        using ArchivedTaskFixture fixture = new();

        TaskItem? found = fixture.Store.FindArchived(Id("HOME-0001"));

        Assert.NotNull(found);
        Assert.Equal(Path.Combine(fixture.Dir.Path, Path.Combine(ArchivedIdSegments)), found.Path);
        Assert.Null(fixture.Store.Get(Id("HOME-0001")));
    }

    /// <summary>A Task closed before the Team was archived (under <c>_closed/</c>) resolves too.</summary>
    [Fact]
    public void FindArchived_ClosedTaskInArchive_Resolves()
    {
        using ArchivedTaskFixture fixture = new();

        TaskItem? found = fixture.Store.FindArchived(Id("HOME-0003"));

        Assert.NotNull(found);
        Assert.True(found.Location.Closed);
    }

    /// <summary>An id that is in neither the live Teams nor the Archive still misses.</summary>
    [Fact]
    public void FindArchived_UnknownId_Misses()
    {
        using ArchivedTaskFixture fixture = new();

        Assert.Null(fixture.Store.FindArchived(Id("HOME-0099")));
    }

    /// <summary>After unarchive the folder is under <c>Teams/</c> again and the id is a live Task, no longer an archived one.</summary>
    [Fact]
    public void Get_AfterUnarchive_ResolvesUnderTeams()
    {
        using ArchivedTaskFixture fixture = new();
        fixture.Store.Dispose();
        Directory.Move(Path.Combine(fixture.Dir.Path, "Archive", "Home"), Path.Combine(fixture.Dir.Path, "Teams", "Home"));

        using TaskStore reopened = TestTaskStore.CreateTaskStore(fixture.Dir, fixture.Personas);

        TaskItem? live = reopened.Get(Id("HOME-0001"));
        Assert.NotNull(live);
        Assert.Equal(Path.Combine(fixture.Dir.Path, "Teams", "Home", "_tasks", "HOME-0001.md"), live.Path);
        Assert.Null(reopened.FindArchived(Id("HOME-0001")));
    }

    /// <summary>The read lookup a tool uses (get_task) returns the archived Task.</summary>
    [Fact]
    public void TryResolveForRead_ArchivedId_ReturnsTask()
    {
        using ArchivedTaskFixture fixture = new();

        bool resolved = TaskToolText.TryResolveForRead(fixture.Store, "HOME-0001", out TaskItem? task, out string refusal);

        Assert.True(resolved);
        Assert.Equal("HOME-0001", task?.Id.ToString());
        Assert.Equal(string.Empty, refusal);
    }

    /// <summary>A write lookup (update, move, comment) for an archived id is refused with the settled text.</summary>
    [Fact]
    public void TryResolve_ArchivedId_RefusesWithUnarchiveText()
    {
        using ArchivedTaskFixture fixture = new();

        bool resolved = TaskToolText.TryResolve(fixture.Store, "HOME-0001", out TaskItem? task, out string refusal);

        Assert.False(resolved);
        Assert.Null(task);
        Assert.Equal("Task HOME-0001 is in an archived Team. Unarchive Home to change it.", refusal);
    }

    /// <summary>A write lookup for an unknown id keeps the old text, and a live id still resolves.</summary>
    [Fact]
    public void TryResolve_UnknownAndLiveIds_Unchanged()
    {
        using ArchivedTaskFixture fixture = new();
        _ = TestTaskStore.WriteTask(TestTaskStore.Root(fixture.Dir), TestTaskStore.RelativePath("Live", null, false, "LIVE-0001.md"), TestTasks.Make(id: "LIVE-0001", location: new TaskLocation("Live", null, false)));
        fixture.Store.Dispose();
        using TaskStore reopened = TestTaskStore.CreateTaskStore(fixture.Dir, fixture.Personas);

        bool live = TaskToolText.TryResolve(reopened, "LIVE-0001", out TaskItem? task, out _);
        bool unknown = TaskToolText.TryResolve(reopened, "HOME-0099", out _, out string refusal);

        Assert.True(live);
        Assert.NotNull(task);
        Assert.False(unknown);
        Assert.Equal("Unknown task 'HOME-0099'.", refusal);
    }

    /// <summary>An archived Team with Tasks, laid out as the archive move leaves it.</summary>
    private sealed class ArchivedTaskFixture : IDisposable
    {
        public ArchivedTaskFixture()
        {
            this.Personas = this.Dir.CreatePersonaStore();
            string archive = Path.Combine(this.Dir.Path, "Archive");
            TaskLocation open = new("Home", null, false);
            TaskLocation closed = new("Home", null, true);
            _ = TestTaskStore.WriteTask(archive, TestTaskStore.RelativePath("Home", null, false, "HOME-0001.md"), TestTasks.Make(id: "HOME-0001", location: open));
            _ = TestTaskStore.WriteTask(archive, TestTaskStore.RelativePath("Home", null, true, "HOME-0003.md"), TestTasks.Make(id: "HOME-0003", status: TaskState.Done, location: closed, closedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            this.Store = TestTaskStore.CreateTaskStore(this.Dir, this.Personas);
        }

        public TempDataDir Dir { get; } = new();

        public PersonaStore Personas { get; }

        public TaskStore Store { get; }

        public void Dispose()
        {
            this.Store.Dispose();
            this.Personas.Dispose();
            this.Dir.Dispose();
        }
    }
}
