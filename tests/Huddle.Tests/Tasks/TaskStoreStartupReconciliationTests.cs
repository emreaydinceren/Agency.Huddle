using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskStore"/>'s startup reconciliation (Spec §8.5, Spec §17 D-16): a Task
/// file edited while Huddle was stopped gains one "edited outside Huddle" Change log entry, no one
/// is woken, and the reconciling write itself is never later reported as an outside edit by the
/// watcher. Every fixture is backdated by <see cref="TestTaskStore.WriteTask"/> so only the file
/// this test deliberately touches triggers reconciliation.
/// </summary>
public sealed class TaskStoreStartupReconciliationTests
{
    /// <summary>A file whose last write time is more than 2 seconds later than its last Change log entry gains exactly one "edited outside Huddle" entry, attributed to <see cref="Agency.Huddle.App.TeamOptions.HumanName"/>, and does not immediately raise <see cref="TaskStore.OutsideEditDetected"/>.</summary>
    [Fact]
    public void Startup_FileNewerThanLastEntry_AppendsOneOutsideEntry_NoEvent()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TaskItem task = TestTasks.Make(
            id: "PLAT-0001",
            location: new("Platform", null, false),
            changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "Human", "created")]);
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), task);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        ManualTimeProvider clock = new() { UtcNow = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero) };

        using TaskStore store = new(dir.Options(), personas, clock, NullLogger<TaskStore>.Instance);
        bool raised = false;
        store.OutsideEditDetected += _ => raised = true;

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem? result = store.Get(id);
        Assert.NotNull(result);
        Assert.Equal(2, result.ChangeLog.Count);
        ChangeLogEntry appended = result.ChangeLog[^1];
        Assert.Equal("You", appended.Actor);
        Assert.Equal("edited outside Huddle", appended.Summary);
        Assert.Equal(clock.UtcNow, appended.At);
        string[] lines = File.ReadAllText(path).TrimEnd().Split('\n');
        Assert.Equal("- 2026-01-15T09:00:00Z | You | edited outside Huddle", lines[^1].TrimEnd('\r'));
        Assert.False(raised);
    }

    /// <summary>A file whose last write time is not newer than its last Change log entry's time (within the 2 second tolerance) is left byte-for-byte unchanged by reconciliation.</summary>
    [Fact]
    public void Startup_FileNotNewer_Unchanged()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TaskItem task = TestTasks.Make(
            id: "PLAT-0001",
            location: new("Platform", null, false),
            changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "Human", "created")]);
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), task);
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        string originalText = File.ReadAllText(path);
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.Equal(originalText, File.ReadAllText(path));
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem? result = store.Get(id);
        Assert.NotNull(result);
        Assert.Single(result.ChangeLog);
    }

    /// <summary>A Task file with no Change log entries at all always gains one "edited outside Huddle" entry at startup, whatever its last write time.</summary>
    [Fact]
    public void Startup_NoEntries_AppendsEntry()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "_tasks", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        ManualTimeProvider clock = new() { UtcNow = new DateTimeOffset(2025, 6, 15, 8, 30, 0, TimeSpan.Zero) };

        using TaskStore store = new(dir.Options(), personas, clock, NullLogger<TaskStore>.Instance);

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem? result = store.Get(id);
        Assert.NotNull(result);
        ChangeLogEntry appended = Assert.Single(result.ChangeLog);
        Assert.Equal("edited outside Huddle", appended.Summary);
        Assert.Equal("You", appended.Actor);
        Assert.Equal(clock.UtcNow, appended.At);
        string[] lines = File.ReadAllText(path).TrimEnd().Split('\n');
        Assert.Equal("- 2025-06-15T08:30:00Z | You | edited outside Huddle", lines[^1].TrimEnd('\r'));
    }

    /// <summary>The atomic write reconciliation makes at startup is recorded as the file's known version, so the watcher's debounced rebuild never later reports it as a separate outside edit (Spec §4 principle 3). Waits 750&#160;ms, a full debounce window and then some, before asserting the negative.</summary>
    [Fact]
    public async Task Startup_ReconcileWrite_NotReportedAsOutsideEdit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string path = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "_tasks", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        bool raised = false;
        store.OutsideEditDetected += _ => raised = true;

        await Task.Delay(TimeSpan.FromMilliseconds(750), ct);

        Assert.False(raised);
    }
}
