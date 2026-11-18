using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Slice 1 of #669 (decision D-6): the Review state is gone from <see cref="TaskStates"/>, and a Task file stored
/// with <c>status: Review</c> loads as In Progress with one Huddle line on its Timeline and no wake. The tests name
/// the state by its wire text only, because the enum member does not exist once the slice is built.
/// </summary>
public sealed class ReviewStateRemovedTests
{
    /// <summary>The states are exactly the eight the spec lists, in order (section 1, item 3).</summary>
    [Fact]
    public void All_Contains_ExactlyTheEightStatesWithoutReview()
    {
        string[] wires = [.. TaskStates.All.Select(static state => state.ToWire())];

        Assert.Equal(
            ["Backlog", "To Do", "In Progress", "Needs You", "Done", "Cancelled", "Duplicate", "Rejected"],
            wires);
    }

    /// <summary>A tool call naming Review no longer parses, whatever its case or spacing (D-6).</summary>
    [Theory]
    [InlineData("Review")]
    [InlineData("review")]
    [InlineData(" REVIEW ")]
    public void TryParse_Review_IsFalse(string wire)
    {
        bool parsed = TaskStates.TryParse(wire, out _);

        Assert.False(parsed);
    }

    /// <summary>A stored <c>status: Review</c> loads as In Progress with a Huddle Timeline line, and loading wakes no one (D-6, section 4.4).</summary>
    [Fact]
    public void Load_StoredStatusReview_ReadsAsInProgressWithTimelineLineAndNoEvent()
    {
        using TempDataDir dir = new();
        string path = WriteStoredReviewTask(dir, tags: []);
        using PersonaStore personas = dir.CreatePersonaStore();

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        bool outsideEdit = false;
        store.OutsideEditDetected += _ => outsideEdit = true;

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem? loaded = store.Get(id);
        Assert.NotNull(loaded);
        Assert.Equal("In Progress", loaded.Status.ToWire());
        TimelineEntry line = Assert.Single(loaded.Timeline, static entry => entry.Author == "Huddle");
        Assert.Contains("Review → In Progress", line.Text, StringComparison.Ordinal); // contains-ok: pins the wording a Human or an Agent reads
        Assert.Contains("Huddle has no Review state", line.Text, StringComparison.Ordinal); // contains-ok: pins the wording a Human or an Agent reads
        Assert.False(outsideEdit);
        Assert.Contains("status: Review", File.ReadAllText(path), StringComparison.Ordinal); // contains-ok: pins the wording a Human or an Agent reads
    }

    /// <summary>A Task that was in Review loads as In Progress and keeps its blockers and tags; only the status changes (D-6).</summary>
    [Fact]
    public void Load_StoredStatusReviewWithTag_KeepsTagsAndReadsAsInProgress()
    {
        using TempDataDir dir = new();
        _ = WriteStoredReviewTask(dir, tags: ["request"]);
        using PersonaStore personas = dir.CreatePersonaStore();

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem? loaded = store.Get(id);
        Assert.NotNull(loaded);
        Assert.Equal("In Progress", loaded.Status.ToWire());
        Assert.Equal(["request"], loaded.Tags);
    }

    /// <summary>Writes Task PLAT-0001 as a file whose <c>status:</c> line says Review, backdated so the store sees no outside edit.</summary>
    private static string WriteStoredReviewTask(TempDataDir dir, IReadOnlyList<string> tags)
    {
        string root = TestTaskStore.Root(dir);
        TaskItem task = TestTasks.Make(
            id: "PLAT-0001",
            status: TaskState.InProgress,
            tags: tags,
            location: new("Platform", null, false),
            changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "Human", "created")]);
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), task);
        string text = File.ReadAllText(path);
        Assert.Contains("status: In Progress", text, StringComparison.Ordinal); // contains-ok: pins the wording a Human or an Agent reads
        File.WriteAllText(path, text.Replace("status: In Progress", "status: Review", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return path;
    }
}
