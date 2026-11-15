using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for startup reconciliation's use of the timeline (Spec §7.7.5, §8.5): the file's write time is compared with the last timeline entry, not the last event.</summary>
public sealed class TaskTimelineReconciliationTests
{
    /// <summary>A file whose last timeline entry is a comment newer than its last event, and written right after that comment, is not reconciled: it would be, were only events compared.</summary>
    [Fact]
    public void Startup_LastEntryIsANewerComment_IsNotReconciled()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TimelineEntry comment = new(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero), "Nova", TimelineEntryKind.Comment, "Later remark.");
        TaskItem task = TestTasks.Make(
            id: "PLAT-0001",
            location: new("Platform", null, false),
            changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "Human", "created")]) with { Comments = [comment] };
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), task);
        File.SetLastWriteTimeUtc(path, comment.At.UtcDateTime.AddSeconds(1));
        string originalText = File.ReadAllText(path);
        using PersonaStore personas = dir.CreatePersonaStore();

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.Equal(originalText, File.ReadAllText(path));
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem? result = store.Get(id);
        Assert.NotNull(result);
        Assert.Single(result.ChangeLog);
        Assert.Equal(comment.At, result.Updated);
    }

    /// <summary>A file written more than 2 seconds after its last timeline entry, a comment, is reconciled: one "edited outside Huddle" event is appended.</summary>
    [Fact]
    public void Startup_FileNewerThanLastComment_AppendsOneOutsideEvent()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TimelineEntry comment = new(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero), "Nova", TimelineEntryKind.Comment, "Later remark.");
        TaskItem task = TestTasks.Make(
            id: "PLAT-0001",
            location: new("Platform", null, false),
            changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "Human", "created")]) with { Comments = [comment] };
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), task);
        File.SetLastWriteTimeUtc(path, comment.At.UtcDateTime.AddSeconds(30));
        using PersonaStore personas = dir.CreatePersonaStore();

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem? result = store.Get(id);
        Assert.NotNull(result);
        Assert.Equal(2, result.ChangeLog.Count);
        Assert.Equal("edited outside Huddle", result.ChangeLog[^1].Summary);
        Assert.Equal(comment, Assert.Single(result.Comments));
    }
}
