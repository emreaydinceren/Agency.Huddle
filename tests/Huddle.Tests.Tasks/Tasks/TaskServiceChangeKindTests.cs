using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Every <see cref="TaskService"/> producer that raises a <see cref="TaskChange"/> with an empty field list says which kind of change it
/// is (Smart wakes spec 3.2 and 5.1), and a comment carries its trimmed text. Nothing reads the kind yet; these tests pin what each
/// producer puts on it.
/// </summary>
public sealed class TaskServiceChangeKindTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The acting Agent.</summary>
    private static TaskActor Nova() => new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>Creates a Task through the harness's real service as the Human and returns it.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness)
    {
        TaskResult result = harness.Service.Create(new TaskDraft("Fix login", "Platform", null), TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>A structured Evidence line by Nova.</summary>
    private static EvidenceLine Line() => new(Noon, "Nova", "dotnet test", 0, null, null, string.Empty);

    /// <summary>Creating a Task in Huddle raises one change of kind Create, with no Before and no comment.</summary>
    [Fact]
    public void Create_InHuddle_RaisesKindCreate()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        _ = CreateTask(harness);

        TaskChange change = Assert.Single(changes);
        Assert.Equal((TaskChangeKind.Create, null, null), (change.Kind, change.Before, change.Comment));
    }

    /// <summary>A Task file that appears outside Huddle raises one change of kind Create with no Before.</summary>
    [Fact]
    public void OutsideCreation_RaisesKindCreate()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        _ = Directory.CreateDirectory(Path.Combine(root, "Platform"));
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskEvents events = new();
        using TaskService service = new(store, new TaskIdAllocator(dir.Options()), events, personas, new EmptySprintCatalog(), dir.Options(), TimeProvider.System, NullLogger<TaskService>.Instance);
        List<TaskChange> changes = [];
        events.TaskChanged += changes.Add;

        _ = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "_tasks", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", title: "Hand made", creator: "Someone", location: new("Platform", null, false)));
        store.RebuildFromWatcher();

        TaskChange change = Assert.Single(changes);
        Assert.Equal((TaskChangeKind.Create, null, TaskActorKind.OutsideHuddle), (change.Kind, change.Before, change.Actor.Kind));
    }

    /// <summary>Evidence raises one change of kind Evidence and no comment text.</summary>
    [Fact]
    public void AddEvidence_RaisesKindEvidence()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        _ = harness.Service.AddEvidence(task.Id, Line(), Nova());

        TaskChange change = Assert.Single(changes);
        Assert.Equal((TaskChangeKind.Evidence, null), (change.Kind, change.Comment));
    }

    /// <summary>A comment raises one change of kind Comment that carries the trimmed comment text, while the Change log summary stays "comment added by".</summary>
    [Fact]
    public void AddComment_RaisesKindCommentWithTheTrimmedText()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        _ = harness.Service.AddComment(task.Id, "  Looks right to me, @Nova.  \n", Nova());

        TaskChange change = Assert.Single(changes);
        Assert.Equal((TaskChangeKind.Comment, "Looks right to me, @Nova.", "comment added by Nova"), (change.Kind, change.Comment, change.Entry.Summary));
    }

    /// <summary>A multi-line comment is carried whole after the trim, its inner line breaks untouched.</summary>
    [Fact]
    public void AddComment_MultiLine_CarriesTheWholeText()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        _ = harness.Service.AddComment(task.Id, "\nFirst line\nSecond line\n\n", Nova());

        Assert.Equal("First line\nSecond line", Assert.Single(changes).Comment?.ReplaceLineEndings("\n"));
    }

    /// <summary>A refused comment (empty text) raises nothing, so no change carries a blank comment.</summary>
    [Fact]
    public void AddComment_EmptyText_RaisesNoChange()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        TaskResult refused = harness.Service.AddComment(task.Id, "   ", Nova());
        _ = harness.Service.AddComment(task.Id, "Real comment", Nova());

        _ = Assert.IsType<TaskResult.Refused>(refused);
        Assert.Equal(["Real comment"], changes.Select(static change => change.Comment));
    }

    /// <summary>Closing raises one change of kind Close and reopening one of kind Reopen, each with an empty field list.</summary>
    [Fact]
    public void CloseAndReopen_RaiseTheirKinds()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        TaskActor human = TaskActors.Human(harness.Options.Value);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Close(task.Id, human));
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Reopen(task.Id, human));

        Assert.Equal([TaskChangeKind.Close, TaskChangeKind.Reopen], changes.Select(static change => change.Kind));
        Assert.All(changes, static change => Assert.Empty(change.Changes));
    }

    /// <summary>Huddle's Sprint step raises one change of kind HuddleStep by Huddle, with an empty field list.</summary>
    [Fact]
    public void RecordHuddleStep_RaisesKindHuddleStep()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        bool written = harness.Service.RecordHuddleStep(task.Id, "approved the Sprint");

        Assert.True(written);
        TaskChange change = Assert.Single(changes);
        Assert.Equal((TaskChangeKind.HuddleStep, TaskActors.Huddle, 0), (change.Kind, change.Actor, change.Changes.Count));
    }

    /// <summary>A release raises one change of kind Release by Huddle, and the Evidence that completed the Proof raises its own change of kind Evidence.</summary>
    [Fact]
    public void Release_RaisesKindRelease()
    {
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem task = rig.CreateInReview();
        List<TaskChange> raised = [];
        rig.Harness.Events.TaskChanged += raised.Add;

        _ = rig.AddEvidence(task);

        TaskChange release = Assert.Single(raised, static change => NeedsYouRelease.IsRelease(change.Entry.Summary));
        TaskChange evidence = Assert.Single(raised, static change => !NeedsYouRelease.IsRelease(change.Entry.Summary));
        Assert.Equal((TaskChangeKind.Release, TaskChangeKind.Evidence), (release.Kind, evidence.Kind));
    }

    /// <summary>A change with fields (an in-app update) keeps the default kind, Edit, and carries no comment, while Evidence on the same Task right after it is Evidence.</summary>
    [Fact]
    public void Update_WithFields_RaisesKindEdit()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Title = "Fix the login" }, null, TaskActors.Human(harness.Options.Value)));
        _ = harness.Service.AddEvidence(task.Id, Line(), Nova());

        Assert.Equal([TaskChangeKind.Edit, TaskChangeKind.Evidence], changes.Select(static change => change.Kind));
        Assert.Equal([TaskField.Title], changes[0].Changes.Select(static field => field.Field));
        Assert.Null(changes[0].Comment);
    }

    /// <summary>A move into <c>_closed/</c> made outside Huddle stays an Edit (it carries its own closed-flag check), and so does the move back out; a second Task file created outside Huddle first is a Create.</summary>
    [Fact]
    public void OutsideCloseAndReopen_StayKindEdit()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string openPath = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "_tasks", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", status: TaskState.Done, creator: "You", description: "Body", location: new("Platform", null, false)));
        string closedPath = Path.Combine(root, "Platform", "_tasks", "_closed", "PLAT-0001.md");
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskEvents events = new();
        using TaskService service = new(store, new TaskIdAllocator(dir.Options()), events, personas, new EmptySprintCatalog(), dir.Options(), TimeProvider.System, NullLogger<TaskService>.Instance);
        List<TaskChange> changes = [];
        events.TaskChanged += changes.Add;

        _ = TestTaskStore.WriteTask(
            root,
            Path.Combine("Platform", "_tasks", "PLAT-0002.md"),
            TestTasks.Make(id: "PLAT-0002", title: "Hand made", creator: "Someone", location: new("Platform", null, false)));
        store.RebuildFromWatcher();
        _ = Directory.CreateDirectory(Path.GetDirectoryName(closedPath) ?? throw new InvalidOperationException("no closed folder"));
        File.Move(openPath, closedPath);
        store.RebuildFromWatcher();
        File.Move(closedPath, openPath);
        store.RebuildFromWatcher();

        Assert.Equal([TaskChangeKind.Create, TaskChangeKind.Edit, TaskChangeKind.Edit], changes.Select(static change => change.Kind));
        Assert.Equal([false, true, false], changes.Select(static change => change.After.Location.Closed));
    }
}
