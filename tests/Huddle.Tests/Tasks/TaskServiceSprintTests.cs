using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Functional tests for <see cref="TaskService"/>'s Sprint handling (Sprints spec §9.2-9.5, SP-T6, SP-I4):
/// validation against <see cref="ISprintCatalog"/>, and the id-and-name labels in the Change log.
/// Real stores over a <see cref="TempDataDir"/>, no mocks.
/// </summary>
public sealed class TaskServiceSprintTests
{
    private static readonly TaskActor HumanActor = new(TaskActorKind.Human, "You", KnownIds.Human);

    private static readonly SprintId S1 = new(1);
    private static readonly SprintId S2 = new(2);
    private static readonly SprintId S9 = new(9);

    private const string TwoSprints = "# Sprints\n\n- S1 | Sprint 1\n- S2 | Auth hardening\n";

    /// <summary>A draft's Sprint is applied by Create and written to the file.</summary>
    [Fact]
    public void Create_KnownSprint_IsStored()
    {
        using Harness h = new(TwoSprints);

        TaskResult result = h.Tasks.Create(new TaskDraft("T", "Platform", null, Sprint: S2), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(S2, saved.Task.Sprint);
        Assert.Contains("sprint: S2", File.ReadAllText(saved.Task.Path), StringComparison.Ordinal);
    }

    /// <summary>An unknown Sprint on Create is Refused with the known list, and nothing is written.</summary>
    [Fact]
    public void Create_UnknownSprint_RefusedWithKnownList()
    {
        using Harness h = new(TwoSprints);

        TaskResult result = h.Tasks.Create(new TaskDraft("T", "Platform", null, Sprint: S9), HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal("Unknown sprint 'S9'. Known sprints: S1 Sprint 1, S2 Auth hardening.", Assert.Single(refused.Problems));
        Assert.Single(h.Store.All);
    }

    /// <summary>With no Sprints at all, the Refused text says so.</summary>
    [Fact]
    public void Create_EmptyCatalogue_SaysThereAreNoSprints()
    {
        using Harness h = new(null);

        TaskResult result = h.Tasks.Create(new TaskDraft("T", "Platform", null, Sprint: S9), HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal("Unknown sprint 'S9'. There are no sprints yet.", Assert.Single(refused.Problems));
    }

    /// <summary>A draft with no Sprint is never checked against the catalogue.</summary>
    [Fact]
    public void Create_NoSprint_NeedsNoCatalogue()
    {
        using Harness h = new(null);

        TaskResult result = h.Tasks.Create(new TaskDraft("T", "Platform", null), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Null(saved.Task.Sprint);
    }

    /// <summary>An update that sets a known Sprint saves, with the change log in the id-and-name form from "none".</summary>
    [Fact]
    public void Update_KnownSprint_SavesAndLogsLabel()
    {
        using Harness h = new(TwoSprints);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(S2), null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(S2, saved.Task.Sprint);
        Assert.Equal("sprint: — → S2 (Auth hardening)", saved.Task.ChangeLog[^1].Summary);
    }

    /// <summary>An update that sets an unknown Sprint is Refused with the known list, and the file is unchanged.</summary>
    [Fact]
    public void Update_UnknownSprint_Refused()
    {
        using Harness h = new(TwoSprints);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(S9), null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal("Unknown sprint 'S9'. Known sprints: S1 Sprint 1, S2 Auth hardening.", Assert.Single(refused.Problems));
        Assert.Null(h.Store.Get(task.Id)?.Sprint);
    }

    /// <summary>An update that sets a Sprint when none exist says there are no sprints yet.</summary>
    [Fact]
    public void Update_EmptyCatalogue_SaysThereAreNoSprints()
    {
        using Harness h = new(null);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(S9), null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal("Unknown sprint 'S9'. There are no sprints yet.", Assert.Single(refused.Problems));
    }

    /// <summary>A Task whose Sprint is missing from the file can still be edited in another field.</summary>
    [Fact]
    public void Update_MissingSprintOnTask_OtherFieldStillEditable()
    {
        using Harness h = new(TwoSprints, seedSprint: S9);
        TaskItem task = h.Seed();
        Assert.Equal(S9, task.Sprint);

        TaskResult result = h.Tasks.Update(task.Id, new TaskPatch { Priority = TaskPriority.Urgent }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskPriority.Urgent, saved.Task.Priority);
        Assert.Equal(S9, saved.Task.Sprint);
    }

    /// <summary>A Sprint not in the file shows as the bare id on the old side of the log line.</summary>
    [Fact]
    public void Update_FromMissingSprint_LogsBareId()
    {
        using Harness h = new(TwoSprints, seedSprint: S9);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(S2), null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("sprint: S9 → S2 (Auth hardening)", saved.Task.ChangeLog[^1].Summary);
    }

    /// <summary>Clearing a Sprint is always valid, even when the catalogue is empty, and the log shows the em dash.</summary>
    [Fact]
    public void Update_ClearSprint_IsValid()
    {
        using Harness h = new(null, seedSprint: S9);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(null), null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Null(saved.Task.Sprint);
        Assert.Equal("sprint: S9 → —", saved.Task.ChangeLog[^1].Summary);
    }

    /// <summary>A Sprint change raises exactly one TaskChanged, whose Changes carry the labels.</summary>
    [Fact]
    public void Update_SprintChange_RaisesOneTaskChangedWithLabels()
    {
        using Harness h = new(TwoSprints, seedSprint: S1);
        TaskItem task = h.Seed();
        List<TaskChange> raised = [];
        h.Events.TaskChanged += raised.Add;

        _ = h.Tasks.Update(task.Id, Set(S2), null, HumanActor);

        TaskChange change = Assert.Single(raised);
        FieldChange field = Assert.Single(change.Changes);
        Assert.Equal(TaskField.Sprint, field.Field);
        Assert.Equal("S1 (Sprint 1)", field.Old);
        Assert.Equal("S2 (Auth hardening)", field.New);
    }

    /// <summary>The log line carries the name of the old Sprint even when it was the last one and its line is removed straight afterwards.</summary>
    [Fact]
    public void Update_LastTaskLeavesSprint_LogKeepsNameAfterRemoval()
    {
        using Harness h = new(TwoSprints, seedSprint: S1);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(S2), null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("sprint: S1 (Sprint 1) → S2 (Auth hardening)", saved.Task.ChangeLog[^1].Summary);
        Assert.Null(h.Sprints.Get(S1));
        Assert.Contains("sprint: S1 (Sprint 1) → S2 (Auth hardening)", File.ReadAllText(saved.Task.Path), StringComparison.Ordinal);
    }

    /// <summary>An edit made outside Huddle is labelled with ids and names the same way.</summary>
    [Fact]
    public void OutsideEdit_SprintChange_LabelledWithIdAndName()
    {
        using Harness h = new(TwoSprints, seedSprint: S1);
        TaskItem task = h.Seed();
        File.WriteAllText(task.Path, TaskFileFormat.Compose(task with { Sprint = S2 }));

        h.Store.RebuildFromWatcher();

        TaskItem written = h.Store.Get(task.Id) ?? throw new InvalidOperationException("task missing");
        Assert.Equal("edited outside Huddle: sprint: S1 (Sprint 1) → S2 (Auth hardening)", written.ChangeLog[^1].Summary);
    }

    /// <summary>Closing and reopening keep the Sprint.</summary>
    [Fact]
    public void CloseAndReopen_KeepSprint()
    {
        using Harness h = new(TwoSprints, seedSprint: S2);
        TaskItem task = h.Seed();

        TaskResult.Saved closed = Assert.IsType<TaskResult.Saved>(h.Tasks.Close(task.Id, HumanActor));
        TaskResult.Saved reopened = Assert.IsType<TaskResult.Saved>(h.Tasks.Reopen(task.Id, HumanActor));

        Assert.Equal(S2, closed.Task.Sprint);
        Assert.Equal(S2, reopened.Task.Sprint);
    }

    /// <summary>Builds a patch that sets the Sprint (or clears it with <see langword="null"/>).</summary>
    /// <param name="sprint">The new Sprint, or <see langword="null"/> to clear.</param>
    private static TaskPatch Set(SprintId? sprint) => new() { Sprint = Optional<SprintId?>.Set(sprint) };

    /// <summary>Real stores over one data directory, wired as the app wires them.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly PersonaStore personas;

        /// <summary>Builds the stores. A non-null <paramref name="sprintsText"/> is written to <c>sprints.md</c> first; a non-null <paramref name="seedSprint"/> seeds one Platform Task holding it.</summary>
        /// <param name="sprintsText">The initial <c>sprints.md</c> text, or <see langword="null"/> for no file.</param>
        /// <param name="seedSprint">The Sprint the seeded Task holds, or <see langword="null"/> for none.</param>
        public Harness(string? sprintsText, SprintId? seedSprint = null)
        {
            if (sprintsText is not null)
            {
                File.WriteAllText(Path.Combine(this.dir.Path, "sprints.md"), sprintsText);
            }

            _ = TestTaskStore.WriteTask(
                TestTaskStore.Root(this.dir),
                TestTaskStore.RelativePath("Platform", null, false, "PLAT-0050.md"),
                TestTasks.Make(id: "PLAT-0050", creator: "You", sprint: seedSprint, location: new("Platform", null, false)));

            ManualTimeProvider clock = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
            this.personas = TestTaskStore.CreatePersonaStore(this.dir);
            _ = this.personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
            this.Store = new TaskStore(this.dir.Options(), this.personas, clock, NullLogger<TaskStore>.Instance);
            this.Events = new TaskEvents();
            this.Sprints = new SprintStore(this.dir.Options(), this.Store, this.Events, NullLogger<SprintStore>.Instance);
            this.Tasks = new TaskService(this.Store, new TaskIdAllocator(this.dir.Options()), this.Events, this.personas, this.Sprints, this.dir.Options(), clock, NullLogger<TaskService>.Instance);
        }

        public TaskStore Store { get; }

        public TaskEvents Events { get; }

        public SprintStore Sprints { get; }

        public TaskService Tasks { get; }

        /// <summary>Returns the seeded Task as stored.</summary>
        public TaskItem Seed() =>
            this.Store.Get(new TaskId("PLAT", 50)) ?? throw new InvalidOperationException("seeded task missing");

        public void Dispose()
        {
            this.Sprints.Dispose();
            this.Tasks.Dispose();
            this.Store.Dispose();
            this.personas.Dispose();
            this.dir.Dispose();
        }
    }
}
