using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ContentFingerprint = Agency.Huddle.App.Tasks.HumanGate.ContentFingerprint;
using IProofSource = Agency.Huddle.App.Tasks.HumanGate.IProofSource;
using NeedsYouTaskEntry = Agency.Huddle.App.NeedsYou.NeedsYouTaskEntry;
using NeedsYouTaskSource = Agency.Huddle.App.NeedsYou.NeedsYouTaskSource;
using Proof = Agency.Huddle.App.Tasks.HumanGate.Proof;

namespace Agency.Huddle.Tests.NeedsYou;

/// <summary>
/// Pins <see cref="NeedsYouTaskSource"/> (Human gate Spec §6.7): the open gated Tasks in Needs You whose acceptance
/// is not valid, oldest move into Needs You first, read from a real <see cref="TaskStore"/> and never held in memory.
/// A decision is expressed by the Task state it produces, because the source only reads state.
/// </summary>
public sealed class NeedsYouTaskSourceTests
{
    private const string Entered = "status: Review → Needs You (Proof 4/4)";
    private const string FirstEntry = "2026-10-01T09:00:00Z";
    private const string SecondEntry = "2026-10-03T09:00:00Z";

    /// <summary>A <see cref="IProofSource"/> that hands back one fixed Proof and remembers which Tasks it was asked about.</summary>
    private sealed class FixedProofSource(Proof proof) : IProofSource
    {
        /// <summary>The ids of the Tasks <see cref="For"/> was called with, in order.</summary>
        public List<string> Asked { get; } = [];

        /// <inheritdoc/>
        public Proof For(TaskItem task)
        {
            this.Asked.Add(task.Id.ToString());
            return proof;
        }
    }

    /// <summary>Seeds a Task on disk, in its Location, with the given gate flags and Change log.</summary>
    /// <param name="harness">The harness whose store receives the Task.</param>
    /// <param name="id">The Task id.</param>
    /// <param name="status">The status.</param>
    /// <param name="gated">True to set the Human gate flag.</param>
    /// <param name="action">The Action, or <see langword="null"/> for none.</param>
    /// <param name="log">The Change log entries.</param>
    private static TaskItem Seed(TaskToolHarness harness, string id, TaskState status, bool gated, string? action, params ChangeLogEntry[] log) =>
        harness.SeedOnDisk(TestTasks.Make(id: id, title: "Write the summary " + id, status: status, description: "Body", changeLog: log) with { HumanGate = gated, Action = action });

    /// <summary>Seeds the common case: a gated Task in Needs You, entered once at <see cref="FirstEntry"/>.</summary>
    /// <param name="harness">The harness whose store receives the Task.</param>
    /// <param name="id">The Task id.</param>
    private static TaskItem SeedWaiting(TaskToolHarness harness, string id = "PLAT-0001") =>
        Seed(harness, id, TaskState.NeedsYou, true, null, TestTasks.Entry(FirstEntry, "Ada", Entered));

    /// <summary>Rewrites a stored Task with a change, as the service would, and returns the re-read Task.</summary>
    /// <param name="harness">The harness whose store is written.</param>
    /// <param name="task">The Task as last read.</param>
    /// <param name="edit">The change to make.</param>
    private static TaskItem Edit(TaskToolHarness harness, TaskItem task, Func<TaskItem, TaskItem> edit)
    {
        TaskItem changed = edit(task);
        return harness.Store.Write(changed, task.Version, TaskFileFormat.Compose(changed))
            ?? throw new InvalidOperationException("The write conflicted.");
    }

    /// <summary>The Change log entry that accepts the Task at its current content.</summary>
    /// <param name="task">The Task being accepted.</param>
    private static ChangeLogEntry Accepted(TaskItem task) =>
        TestTasks.Entry("2026-10-02T09:00:00Z", "Emre", "accepted " + ContentFingerprint.Of(task.Title, task.Action, task.Description));

    /// <summary>Builds a source over the harness's store and events with the given Proof source.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="proofs">The Proof source.</param>
    private static NeedsYouTaskSource SourceOver(TaskToolHarness harness, IProofSource proofs) => new(harness.Store, proofs, harness.Events);

    /// <summary>A gated Task in Needs You is listed with its id, title, the time it entered Needs You and its Proof.</summary>
    [Fact]
    public void GatedTaskInNeedsYou_IsListed()
    {
        using TaskToolHarness harness = new();
        TaskItem task = SeedWaiting(harness);
        Proof proof = new([]);
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(proof));

        NeedsYouTaskEntry entry = Assert.Single(source.Entries);

        Assert.Equal(task.Id, entry.TaskId);
        Assert.Equal("Write the summary PLAT-0001", entry.Title);
        Assert.Equal(DateTimeOffset.Parse(FirstEntry, System.Globalization.CultureInfo.InvariantCulture), entry.Since);
        Assert.Same(proof, entry.Proof);
    }

    /// <summary>A Task gated only by an Action (no Human gate flag) is gated too, and is listed.</summary>
    [Fact]
    public void TaskGatedByAnAction_IsListed()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Seed(harness, "PLAT-0001", TaskState.NeedsYou, false, "Read the summary", TestTasks.Entry(FirstEntry, "Ada", Entered));
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));

        Assert.Equal(task.Id, Assert.Single(source.Entries).TaskId);
    }

    /// <summary>The Proof of each entry comes from the Proof source, asked about that Task.</summary>
    [Fact]
    public void Entries_AsksTheProofSourceAboutEachListedTask()
    {
        using TaskToolHarness harness = new();
        _ = SeedWaiting(harness);
        FixedProofSource proofs = new(new Proof([]));
        using NeedsYouTaskSource source = SourceOver(harness, proofs);

        _ = source.Entries;

        Assert.Equal(["PLAT-0001"], proofs.Asked);
    }

    /// <summary>An ordinary Task (no gate, no Action) in Needs You is never listed.</summary>
    [Fact]
    public void OrdinaryTaskInNeedsYou_IsNotListed()
    {
        using TaskToolHarness harness = new();
        _ = Seed(harness, "PLAT-0001", TaskState.NeedsYou, false, null, TestTasks.Entry(FirstEntry, "Ada", Entered));
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));

        Assert.Empty(source.Entries);
    }

    /// <summary>A gated Task that is not in Needs You (still in Review) is not listed.</summary>
    [Fact]
    public void GatedTaskInReview_IsNotListed()
    {
        using TaskToolHarness harness = new();
        _ = Seed(harness, "PLAT-0001", TaskState.InProgress, true, null, TestTasks.Entry(FirstEntry, "Ada", "created"));
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));

        Assert.Empty(source.Entries);
    }

    /// <summary>Accept on a Task without an Action closes it as Done, and it leaves the list.</summary>
    [Fact]
    public void Accept_WithoutAction_TaskIsDone_LeavesTheList()
    {
        using TaskToolHarness harness = new();
        TaskItem task = SeedWaiting(harness);
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        Assert.Single(source.Entries);

        _ = Edit(harness, task, t => t with { Status = TaskState.Done });

        Assert.Empty(source.Entries);
    }

    /// <summary>Accept on a Task with an Action leaves it in Needs You with a valid acceptance, and it leaves the list.</summary>
    [Fact]
    public void Accept_WithAction_ValidAcceptance_IsNotListed()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Seed(harness, "PLAT-0001", TaskState.NeedsYou, false, "Read the summary", TestTasks.Entry(FirstEntry, "Ada", Entered));
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        Assert.Single(source.Entries);

        TaskItem accepted = Edit(harness, task, t => t with { ChangeLog = [.. t.ChangeLog, Accepted(t)] });

        Assert.Equal(TaskState.NeedsYou, accepted.Status);
        Assert.Empty(source.Entries);
    }

    /// <summary>Editing an accepted Task makes the acceptance Stale and puts the Task back on the list.</summary>
    [Fact]
    public void AcceptedTask_EditedAfterwards_IsListedAgain()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Seed(harness, "PLAT-0001", TaskState.NeedsYou, false, "Read the summary", TestTasks.Entry(FirstEntry, "Ada", Entered));
        task = Edit(harness, task, t => t with { ChangeLog = [.. t.ChangeLog, Accepted(t)] });
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        Assert.Empty(source.Entries);

        _ = Edit(harness, task, t => t with { Description = "Body, reworded" });

        Assert.Equal("PLAT-0001", Assert.Single(source.Entries).TaskId.ToString());
    }

    /// <summary>Request changes sends the Task back to In Progress, and it leaves the list.</summary>
    [Fact]
    public void RequestChanges_TaskIsInProgress_LeavesTheList()
    {
        using TaskToolHarness harness = new();
        TaskItem task = SeedWaiting(harness);
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        Assert.Single(source.Entries);

        _ = Edit(harness, task, t => t with { Status = TaskState.InProgress });

        Assert.Empty(source.Entries);
    }

    /// <summary>Decline rejects the Task, and it leaves the list.</summary>
    [Fact]
    public void Decline_TaskIsRejected_LeavesTheList()
    {
        using TaskToolHarness harness = new();
        TaskItem task = SeedWaiting(harness);
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        Assert.Single(source.Entries);

        _ = Edit(harness, task, t => t with { Status = TaskState.Rejected });

        Assert.Empty(source.Entries);
    }

    /// <summary>A move back to Review takes the Task off the list.</summary>
    [Fact]
    public void MoveBackToReview_LeavesTheList()
    {
        using TaskToolHarness harness = new();
        TaskItem task = SeedWaiting(harness);
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        Assert.Single(source.Entries);

        _ = Edit(harness, task, t => t with { Status = TaskState.InProgress });

        Assert.Empty(source.Entries);
    }

    /// <summary>The oldest move into Needs You comes first, whatever order the Tasks were created in.</summary>
    [Fact]
    public void Entries_AreOrderedOldestSinceFirst()
    {
        using TaskToolHarness harness = new();
        _ = Seed(harness, "PLAT-0001", TaskState.NeedsYou, true, null, TestTasks.Entry(SecondEntry, "Ada", Entered));
        _ = Seed(harness, "PLAT-0002", TaskState.NeedsYou, true, null, TestTasks.Entry(FirstEntry, "Ada", Entered));
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));

        Assert.Equal(["PLAT-0002", "PLAT-0001"], source.Entries.Select(entry => entry.TaskId.ToString()));
    }

    /// <summary>When a Task has entered Needs You more than once, Since is the latest entry.</summary>
    [Fact]
    public void Since_IsTheLatestMoveIntoNeedsYou()
    {
        using TaskToolHarness harness = new();
        _ = Seed(
            harness,
            "PLAT-0001",
            TaskState.NeedsYou,
            true,
            null,
            TestTasks.Entry(FirstEntry, "Ada", Entered),
            TestTasks.Entry("2026-10-02T09:00:00Z", "Emre", "status: Needs You → In Progress"),
            TestTasks.Entry(SecondEntry, "Ada", Entered));
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));

        Assert.Equal(DateTimeOffset.Parse(SecondEntry, System.Globalization.CultureInfo.InvariantCulture), Assert.Single(source.Entries).Since);
    }

    /// <summary>The source holds no list of its own: a store change shows on the next read with no event raised.</summary>
    [Fact]
    public void Entries_ReadTheStoreOnEveryCall()
    {
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        Assert.Empty(source.Entries);

        _ = SeedWaiting(harness);

        Assert.Single(source.Entries);
    }

    /// <summary>A new store over the same folder lists the same entries, so a restart loses nothing.</summary>
    [Fact]
    public void NewStoreOverSameFolder_ListsTheSameEntries()
    {
        using TaskToolHarness harness = new();
        _ = SeedWaiting(harness, "PLAT-0001");
        _ = Seed(harness, "PLAT-0002", TaskState.NeedsYou, true, null, TestTasks.Entry(SecondEntry, "Ada", Entered));
        Proof proof = new([]);
        using NeedsYouTaskSource before = SourceOver(harness, new FixedProofSource(proof));
        using TaskStore reopened = new(harness.Options, harness.Personas, harness.Clock, NullLogger<TaskStore>.Instance);
        using NeedsYouTaskSource after = new(reopened, new FixedProofSource(proof), new TaskEvents());

        Assert.Equal(2, after.Entries.Count);
        Assert.Equal(before.Entries, after.Entries);
    }

    /// <summary>One <see cref="TaskEvents.TaskChanged"/> raises <see cref="NeedsYouTaskSource.Changed"/> exactly once.</summary>
    [Fact]
    public void TaskChanged_RaisesChangedOnce()
    {
        using TaskToolHarness harness = new();
        TaskItem task = SeedWaiting(harness);
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        int changes = 0;
        source.Changed += () => changes++;

        harness.Events.RaiseTaskChanged(new TaskChange(task, task, [], harness.HumanActor(), task.ChangeLog[^1]));

        Assert.Equal(1, changes);
    }

    /// <summary>One <see cref="TaskEvents.TasksReloaded"/> raises <see cref="NeedsYouTaskSource.Changed"/> exactly once.</summary>
    [Fact]
    public void TasksReloaded_RaisesChangedOnce()
    {
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        int changes = 0;
        source.Changed += () => changes++;

        harness.Events.RaiseTasksReloaded();

        Assert.Equal(1, changes);
    }

    /// <summary>After <see cref="NeedsYouTaskSource.Dispose"/> neither Task event raises <see cref="NeedsYouTaskSource.Changed"/>.</summary>
    [Fact]
    public void Dispose_UnsubscribesFromTaskEvents()
    {
        using TaskToolHarness harness = new();
        TaskItem task = SeedWaiting(harness);
        NeedsYouTaskSource source = SourceOver(harness, new FixedProofSource(new Proof([])));
        int changes = 0;
        source.Changed += () => changes++;
        source.Dispose();

        harness.Events.RaiseTaskChanged(new TaskChange(task, task, [], harness.HumanActor(), task.ChangeLog[^1]));
        harness.Events.RaiseTasksReloaded();

        Assert.Equal(0, changes);
    }
}
