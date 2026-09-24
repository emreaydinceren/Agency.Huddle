using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskService"/>'s outside-edit handler - the D6/§9.4 logic that turns a
/// <see cref="TaskStore.OutsideEditDetected"/> notification into a Change log entry - and for
/// <see cref="TaskService"/>'s new <see cref="IDisposable"/> implementation (Spec §9.4,
/// corrections-B2 D6 item 8, Task 6.5). Drives <see cref="TaskStore.RebuildFromWatcher"/>
/// synchronously wherever possible, and the real watcher only for the two tests that need actual
/// elapsed time.
/// </summary>
public sealed class OutsideEditLoggingTests
{
    /// <summary>A field edited by hand gets an "edited outside Huddle: " prefixed summary, attributed to the Human's configured Name (Spec §9.4).</summary>
    [Fact]
    public void OutsideEdit_Priority_AppendsPrefixedEntryAsHuman()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", creator: "You", priority: TaskPriority.High, location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        File.WriteAllText(path, TaskFileFormat.Compose(original with { Priority = TaskPriority.Urgent }));

        store.RebuildFromWatcher();

        TaskItem? written = store.Get(id);
        Assert.NotNull(written);
        ChangeLogEntry lastEntry = written.ChangeLog[^1];
        Assert.Equal("edited outside Huddle: priority: High → Urgent", lastEntry.Summary);
        Assert.Equal("You", lastEntry.Actor);
    }

    /// <summary>An unusual key order and an unknown key in the hand-edited file survive byte for byte, apart from the one appended Change log line.</summary>
    [Fact]
    public void OutsideEdit_KeepsHumansFormatting()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", title: "T", creator: "You", priority: TaskPriority.Medium, location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        const string HandEditedText =
            "---\n" +
            "title: 'T'\n" +
            "id: PLAT-0001\n" +
            "owner: Alice\n" +
            "status: To Do\n" +
            "priority: Urgent\n" +
            "creator: You\n" +
            "---\n" +
            "Desc.\n" +
            "\n" +
            "## Change log\n" +
            "- 2026-01-01T00:00:00Z | You | created\n";
        File.WriteAllText(path, HandEditedText);

        store.RebuildFromWatcher();

        string finalText = File.ReadAllText(path);
        int headingIndex = HandEditedText.IndexOf(TaskFileFormat.ChangeLogHeading, StringComparison.Ordinal);
        string expectedUnchangedHead = HandEditedText[..headingIndex];
        Assert.StartsWith(expectedUnchangedHead, finalText, StringComparison.Ordinal);
        Assert.Contains("owner: Alice", finalText, StringComparison.Ordinal);

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem? reparsed = store.Get(id);
        Assert.NotNull(reparsed);
        Assert.Equal(2, reparsed.ChangeLog.Count);
    }

    /// <summary>TaskChanged is raised exactly once, with an OutsideHuddle actor named after the Human.</summary>
    [Fact]
    public void OutsideEdit_RaisesTaskChangedWithOutsideHuddleActor()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", priority: TaskPriority.High, location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);

        List<TaskChange> changes = [];
        events.TaskChanged += changes.Add;

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        File.WriteAllText(path, TaskFileFormat.Compose(original with { Priority = TaskPriority.Urgent }));

        store.RebuildFromWatcher();

        TaskChange change = Assert.Single(changes);
        Assert.Equal(TaskActorKind.OutsideHuddle, change.Actor.Kind);
        Assert.Equal("You", change.Actor.Name);
        Assert.Equal(KnownIds.Human, change.Actor.UserId);
    }

    /// <summary>A hand edit that makes the file invalid leaves it a rejected file: no entry is appended and no event is raised (Spec §9.4).</summary>
    [Fact]
    public void OutsideEdit_InvalidFile_NoEntryNoEvent()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);

        int raiseCount = 0;
        events.TaskChanged += _ => raiseCount++;

        const string InvalidText =
            "---\n" +
            "id: PLAT-0001\n" +
            "status: To Do\n" +
            "priority: Medium\n" +
            "creator: You\n" +
            "---\n" +
            "No title key at all.\n" +
            "\n" +
            "## Change log\n" +
            "- 2026-01-01T00:00:00Z | You | created\n";
        File.WriteAllText(path, InvalidText);

        store.RebuildFromWatcher();

        Assert.Equal(InvalidText, File.ReadAllText(path));
        Assert.Equal(0, raiseCount);
        Assert.Contains(store.RejectedFiles, rejected => string.Equals(rejected.Path, path, StringComparison.Ordinal));
    }

    /// <summary>A file that appears where none existed before (Before is null) gets the bare summary "edited outside Huddle", with no diff suffix (Settled corrections-B2 D6 item 8).</summary>
    [Fact]
    public void OutsideEdit_CreatedByHand_BareEntry()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", title: "Hand made", creator: "Someone", location: new("Platform", null, false)));

        store.RebuildFromWatcher();

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem? written = store.Get(id);
        Assert.NotNull(written);
        ChangeLogEntry lastEntry = written.ChangeLog[^1];
        Assert.Equal("edited outside Huddle", lastEntry.Summary);
        Assert.Equal("You", lastEntry.Actor);
    }

    /// <summary>When the disk version moves again between the watcher's detection and the service's append, <see cref="TaskStore.AppendEntry"/> returns null and the service skips silently: no write, no TaskChanged (Settled corrections-B2 D6 item 8, "null means skip").</summary>
    [Fact]
    public void OutsideEdit_VersionMovedBeforeAppend_SkipsWithoutEvent()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(
                id: "PLAT-0001",
                priority: TaskPriority.Medium,
                location: new("Platform", null, false),
                changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);

        // Subscribed before TaskService, so this handler runs first for every edit and races a
        // second disk write in before the service gets to append against the version it saw.
        store.OutsideEditDetected += _ =>
        {
            TaskItem racedBase = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
            store.Write(racedBase, racedBase.Version, TaskFileFormat.Compose(racedBase with { Title = "raced" }));
        };

        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);
        List<TaskChange> changes = [];
        events.TaskChanged += changes.Add;

        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        File.WriteAllText(path, TaskFileFormat.Compose(original with { Priority = TaskPriority.Urgent }));

        store.RebuildFromWatcher();

        Assert.Empty(changes);
        TaskItem? afterRace = store.Get(id);
        Assert.NotNull(afterRace);
        Assert.Equal("raced", afterRace.Title);
        Assert.DoesNotContain("edited outside Huddle", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>A Team folder move made by hand is logged with the same "moved: Old → New" vocabulary Update uses for a Team change (Spec §7.5, Spec §9.4).</summary>
    [Fact]
    public void OutsideEdit_TeamMovedByHand_LogsMoved()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string sourcePath = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", creator: "You", location: new("Platform", null, false)));
        Directory.CreateDirectory(Path.Combine(root, "Support"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        string targetPath = Path.Combine(root, "Support", "PLAT-0001.md");
        File.WriteAllText(targetPath, TaskFileFormat.Compose(original with { Location = new("Support", null, false) }));
        File.Delete(sourcePath);

        store.RebuildFromWatcher();

        TaskItem? moved = store.Get(id);
        Assert.NotNull(moved);
        ChangeLogEntry lastEntry = moved.ChangeLog[^1];
        Assert.Equal("edited outside Huddle: moved: Platform → Support", lastEntry.Summary);
    }

    /// <summary>The service's own append (updating lastSeenVersion as any store write does) is never itself reported as a further outside edit - waits a full debounce window and then some before asserting the negative, like <c>OwnWrite_NotReportedAsOutsideEdit</c> does at the store level.</summary>
    [Fact]
    public async Task OutsideEdit_OwnAppend_NotReportedAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", priority: TaskPriority.Medium, location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);

        int raiseCount = 0;
        TaskCompletionSource firstRaised = new(TaskCreationOptions.RunContinuationsAsynchronously);
        events.TaskChanged += _ =>
        {
            raiseCount++;
            firstRaised.TrySetResult();
        };

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        await File.WriteAllTextAsync(path, TaskFileFormat.Compose(original with { Priority = TaskPriority.Urgent }), ct);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => firstRaised.TrySetCanceled());
        await firstRaised.Task;

        await Task.Delay(TimeSpan.FromMilliseconds(750), ct);

        Assert.Equal(1, raiseCount);
    }

    /// <summary>Disposing the service unsubscribes from both <see cref="TaskStore.OutsideEditDetected"/> and <see cref="TaskStore.IndexChanged"/>, leaving neither event with any invocation list at all (Settled corrections-B2 D6 item 8, traps.md L98).</summary>
    [Fact]
    public void Dispose_UnsubscribesFromStoreEvents()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        service.Dispose();

        Assert.Null(GetEventBackingField(store, nameof(TaskStore.OutsideEditDetected)));
        Assert.Null(GetEventBackingField(store, nameof(TaskStore.IndexChanged)));
    }

    /// <summary>Reads a public event's compiler-generated backing field via reflection, so "no subscribers" can be proven directly rather than by an event's side effect.</summary>
    /// <param name="store">The instance whose event field is inspected.</param>
    /// <param name="eventName">The event's name, and its backing field's name.</param>
    private static Delegate? GetEventBackingField(TaskStore store, string eventName)
    {
        FieldInfo field = typeof(TaskStore).GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"No backing field named '{eventName}' on TaskStore."));
        return (Delegate?)field.GetValue(store);
    }

    /// <summary>Constructs the <see cref="TaskService"/> under test, with a fresh <see cref="TaskEvents"/> hub unless <paramref name="events"/> is supplied.</summary>
    private static TaskService CreateTaskService(TempDataDir dir, TaskStore store, PersonaStore personas, TaskEvents? events = null) =>
        new(store, new TaskIdAllocator(dir.Options()), events ?? new TaskEvents(), personas, dir.Options(), TimeProvider.System, NullLogger<TaskService>.Instance);
}
