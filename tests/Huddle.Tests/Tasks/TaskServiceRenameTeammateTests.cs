using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskService.RenameTeammate"/> (Spec §9.6, corrections-B2 D6 items 10-11):
/// rewriting <c>creator:</c>/<c>assignee:</c> in every matching Task, including Closed ones, with
/// no Change log entry and no <see cref="TaskEvents.TaskChanged"/> - it is a change of identity,
/// not of the Task (ADR-0011). Also covers the R3 lock-order re-entrancy test and the R4 eager
/// construction test that this task's plan section names alongside it.
/// </summary>
public sealed class TaskServiceRenameTeammateTests
{
    private static readonly TaskActor HumanActor = new(TaskActorKind.Human, "You", KnownIds.Human);

    /// <summary>Rewrites assignee on an open Task and creator on a Closed one, matching Spec §9.6's "including Closed ones" and leaving the Closed location untouched.</summary>
    [Fact]
    public void RenameTeammate_RewritesCreatorAndAssignee_IncludingClosed()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(
                id: "PLAT-0001",
                assignee: "Nova",
                creator: "You",
                location: new("Platform", null, false),
                changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        TestTaskStore.WriteTask(
            root,
            TestTaskStore.RelativePath("Platform", null, closed: true, "PLAT-0002.md"),
            TestTasks.Make(
                id: "PLAT-0002",
                assignee: "You",
                creator: "Nova",
                location: new("Platform", null, true),
                changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "Nova", "created")]));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        service.RenameTeammate("Nova", "NovaPrime");

        _ = TaskId.TryParse("PLAT-0001", out TaskId openId);
        _ = TaskId.TryParse("PLAT-0002", out TaskId closedId);
        TaskItem openTask = store.Get(openId) ?? throw new InvalidOperationException("fixture task missing");
        TaskItem closedTask = store.Get(closedId) ?? throw new InvalidOperationException("fixture task missing");
        Assert.Equal("NovaPrime", openTask.Assignee);
        Assert.Equal("NovaPrime", closedTask.Creator);
        Assert.True(closedTask.Location.Closed);
    }

    /// <summary>Spec §9.6: the rename changes the files with no Change log entry and no TaskChanged event - a change of identity, not of the Task.</summary>
    [Fact]
    public void RenameTeammate_NoChangeLogEntry_NoTaskChanged()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(
                id: "PLAT-0001",
                assignee: "Nova",
                location: new("Platform", null, false),
                changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskEvents events = new();
        using TaskService service = CreateTaskService(dir, store, personas, events);

        int raiseCount = 0;
        events.TaskChanged += _ => raiseCount++;

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem before = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        int changeLogCountBefore = before.ChangeLog.Count;

        service.RenameTeammate("Nova", "NovaPrime");

        TaskItem after = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        Assert.Equal(changeLogCountBefore, after.ChangeLog.Count);
        Assert.Equal(0, raiseCount);
    }

    /// <summary>Renaming across several Tasks in one call raises TasksReloaded exactly once, not once per file (Spec §9.6, uses TaskStore.WriteMany per corrections-B2 D5 item 7).</summary>
    [Fact]
    public void RenameTeammate_RaisesTasksReloadedOnce()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", assignee: "Nova", location: new("Platform", null, false), changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0002.md"),
            TestTasks.Make(id: "PLAT-0002", assignee: "Nova", location: new("Platform", null, false), changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskEvents events = new();
        using TaskService service = CreateTaskService(dir, store, personas, events);

        int reloadCount = 0;
        events.TasksReloaded += () => reloadCount++;

        service.RenameTeammate("Nova", "NovaPrime");

        Assert.Equal(1, reloadCount);
    }

    /// <summary>ADR-0011: a rename moves the Teammate, not its history - an existing Change log line naming the old Name keeps it, even though the Task's own Assignee field is rewritten.</summary>
    [Fact]
    public void RenameTeammate_OldLogLinesKeepOldName()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(
                id: "PLAT-0001",
                assignee: "Nova",
                location: new("Platform", null, false),
                changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "Nova", "created")]));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        service.RenameTeammate("Nova", "NovaPrime");

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem after = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        ChangeLogEntry entry = Assert.Single(after.ChangeLog);
        Assert.Equal("Nova", entry.Actor);
        Assert.Equal("NovaPrime", after.Assignee);
    }

    /// <summary>Matches the old Name case-insensitively (Spec §9.6).</summary>
    [Fact]
    public void RenameTeammate_MatchesIgnoringCase()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(
                id: "PLAT-0001",
                creator: "Nova",
                location: new("Platform", null, false),
                changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        service.RenameTeammate("nova", "NovaPrime");

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem after = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        Assert.Equal("NovaPrime", after.Creator);
    }

    /// <summary>
    /// One Task's file having raced out from under the rename (simulated the same way as the
    /// outside-edit race test: its in-memory version is stale by the time the rename writes) is
    /// skipped, but does not stop the other Task from being renamed, and never throws (corrections-B2
    /// D6 item 11: "RenameTeammate catches per file and continues; it never throws").
    /// </summary>
    [Fact]
    public void RenameTeammate_OneFileFails_OthersRenamed_NoThrow()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string racedPath = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", assignee: "Nova", location: new("Platform", null, false), changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0002.md"),
            TestTasks.Make(id: "PLAT-0002", assignee: "Nova", location: new("Platform", null, false), changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        _ = TaskId.TryParse("PLAT-0001", out TaskId racedId);
        TaskItem staleRaced = store.Get(racedId) ?? throw new InvalidOperationException("fixture task missing");

        // A race: the file changes on disk without the store's in-memory index (or this service,
        // which reads through it) ever seeing the new version - the same technique
        // OutsideEditLoggingTests uses to simulate a version moving out from under a writer.
        File.WriteAllText(racedPath, TaskFileFormat.Compose(staleRaced with { Title = "raced" }));

        service.RenameTeammate("Nova", "NovaPrime");

        _ = TaskId.TryParse("PLAT-0002", out TaskId otherId);
        TaskItem other = store.Get(otherId) ?? throw new InvalidOperationException("fixture task missing");
        Assert.Equal("NovaPrime", other.Assignee);
    }

    /// <summary>
    /// R5: the rename's own writes update the store's <c>lastSeenVersion</c> the same way any other
    /// write does, so a watcher rebuild straight afterward must not mistake them for an edit made
    /// outside Huddle.
    /// </summary>
    [Fact]
    public void RenameTeammate_ThenWatcherRebuild_NoOutsideEditEntry()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", assignee: "Nova", location: new("Platform", null, false), changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        service.RenameTeammate("Nova", "NovaPrime");
        store.RebuildFromWatcher();

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem after = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        Assert.DoesNotContain(after.ChangeLog, entry => entry.Summary.Contains("edited outside Huddle", StringComparison.Ordinal));
    }

    /// <summary>
    /// R3 lock order: <c>TaskService.mutateGate</c> -&gt; <c>TaskStore.writeGate</c>, and
    /// <see cref="TaskService"/> never calls <see cref="PersonaStore"/> or raises an event while
    /// holding <c>writeGate</c>. Proven here the other way around: a
    /// <see cref="PersonaStore.PersonasChanged"/> handler that calls back into
    /// <see cref="TaskService.Create"/> from another thread must finish - no lock TaskService takes
    /// is ever held across a call that could still be waiting on this handler.
    /// </summary>
    [Fact]
    public async Task PersonasChangedHandler_CallsBackIntoTaskServiceFromAnotherThread_FinishesWithin10Seconds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using TaskService service = CreateTaskService(dir, store, personas);

        TaskCompletionSource<TaskResult> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        personas.PersonasChanged += () =>
        {
            _ = Task.Run(() =>
            {
                TaskResult result = service.Create(new TaskDraft("Reentrant", "Platform", null), HumanActor);
                completed.TrySetResult(result);
            });
        };

        personas.Add(new PersonaIdentity("Echo", "Echo", "echo", ["Platform"]), "You are Echo.");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => completed.TrySetCanceled());

        TaskResult result = await completed.Task;

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>
    /// Settled corrections-B2 D6 item 10: TaskService is a lazy singleton until 6.6.i injects it (and
    /// ViewStore) into the hosted PersonaRenameCascade, so the composed application's host must now
    /// construct it at startup even though nothing in this test ever resolves TaskService directly -
    /// proven by reflecting TaskStore.OutsideEditDetected's backing field, which only gains a
    /// subscriber once TaskService's constructor has actually run.
    /// </summary>
    [Fact]
    public async Task TaskService_ConstructedAtStartup()
    {
        await using TeamWebApplicationFactory factory = new();

        TaskStore store = factory.Services.GetRequiredService<TaskStore>();

        Assert.NotNull(GetEventBackingField(store, nameof(TaskStore.OutsideEditDetected)));
    }

    /// <summary>Reads a public event's compiler-generated backing field via reflection, so "someone subscribed" can be proven directly rather than by an event's side effect.</summary>
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
