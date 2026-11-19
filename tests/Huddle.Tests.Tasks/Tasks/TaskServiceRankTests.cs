using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Functional tests for <see cref="TaskService"/>'s handling of <c>rank</c>: Create and Update carry it,
/// a value below 1 is refused, and a repeated value is Unchanged. Real stores over a <see cref="TempDataDir"/>.
/// </summary>
public sealed class TaskServiceRankTests
{
    private static readonly TaskActor HumanActor = new(TaskActorKind.Human, "You", KnownIds.Human);

    /// <summary>A draft's rank is stored by Create and written to the file.</summary>
    [Fact]
    public void Create_Rank_IsStored()
    {
        using Harness h = new(null);

        TaskResult result = h.Tasks.Create(new TaskDraft("T", "Platform", null) { Rank = 4 }, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(4, saved.Task.Rank);
        // contains-ok: the haystack is the Task file on disk, and the assert pins the one `rank:` front-matter line, not rendered markup.
        Assert.Contains("rank: 4", File.ReadAllText(saved.Task.Path), StringComparison.Ordinal);
    }

    /// <summary>A draft with no rank creates an unranked Task.</summary>
    [Fact]
    public void Create_NoRank_IsUnranked()
    {
        using Harness h = new(null);

        TaskResult result = h.Tasks.Create(new TaskDraft("T", "Platform", null), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Null(saved.Task.Rank);
    }

    /// <summary>A draft whose rank is below 1 is refused with the rank problem, and nothing is written.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RankBelowOne_Refused(int rank)
    {
        using Harness h = new(null);

        TaskResult result = h.Tasks.Create(new TaskDraft("T", "Platform", null) { Rank = rank }, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal("Rank must be a whole number of 1 or more.", Assert.Single(refused.Problems));
        Assert.Single(h.Store.All);
    }

    /// <summary>An update that sets a rank on an unranked Task saves, logging <c>rank: — → 3</c>.</summary>
    [Fact]
    public void Update_SetRank_SavesAndLogs()
    {
        using Harness h = new(null);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(3), null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(3, saved.Task.Rank);
        Assert.Equal("rank: — → 3", saved.Task.ChangeLog[^1].Summary);
        Assert.Equal(3, h.Store.Get(task.Id)?.Rank);
    }

    /// <summary>An update that changes a rank raises exactly one TaskChanged carrying one Rank field change.</summary>
    [Fact]
    public void Update_ChangeRank_RaisesOneTaskChangedWithRankField()
    {
        using Harness h = new(2);
        TaskItem task = h.Seed();
        List<TaskChange> raised = [];
        h.Events.TaskChanged += raised.Add;

        _ = h.Tasks.Update(task.Id, Set(8), null, HumanActor);

        TaskChange change = Assert.Single(raised);
        FieldChange field = Assert.Single(change.Changes);
        Assert.Equal(TaskField.Rank, field.Field);
        Assert.Equal("2", field.Old);
        Assert.Equal("8", field.New);
    }

    /// <summary>An update that sets a rank below 1 is refused with the rank problem and the file keeps its rank.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_RankBelowOne_Refused(int rank)
    {
        using Harness h = new(5);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(rank), null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal("Rank must be a whole number of 1 or more.", Assert.Single(refused.Problems));
        Assert.Equal(5, h.Store.Get(task.Id)?.Rank);
    }

    /// <summary>Clearing a rank with <see langword="null"/> is valid and logs the dash on the new side.</summary>
    [Fact]
    public void Update_ClearRank_IsValid()
    {
        using Harness h = new(5);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(null), null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Null(saved.Task.Rank);
        Assert.Equal("rank: 5 → —", saved.Task.ChangeLog[^1].Summary);
    }

    /// <summary>Setting the rank a Task already has changes nothing and is reported Unchanged.</summary>
    [Fact]
    public void Update_SameRank_IsUnchanged()
    {
        using Harness h = new(5);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, Set(5), null, HumanActor);

        Assert.IsType<TaskResult.Unchanged>(result);
    }

    /// <summary>A rank-only patch is not empty and names the Rank field.</summary>
    [Fact]
    public void Patch_RankOnly_IsNotEmptyAndNamesRank()
    {
        TaskPatch patch = Set(3);

        Assert.False(patch.IsEmpty);
        Assert.Equal([TaskField.Rank], patch.Fields());
        Assert.Equal(3, patch.ApplyTo(TestTasks.Make()).Rank);
    }

    /// <summary>A patch that does not mention rank leaves it alone, and a Task with a rank keeps it across another edit.</summary>
    [Fact]
    public void Update_OtherField_KeepsRank()
    {
        using Harness h = new(6);
        TaskItem task = h.Seed();

        TaskResult result = h.Tasks.Update(task.Id, new TaskPatch { Priority = TaskPriority.Urgent }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(6, saved.Task.Rank);
    }

    /// <summary>Builds a patch that sets the rank (or clears it with <see langword="null"/>).</summary>
    /// <param name="rank">The new rank, or <see langword="null"/> to clear.</param>
    private static TaskPatch Set(int? rank) => new() { Rank = Optional<int?>.Set(rank) };

    /// <summary>Real stores over one data directory, wired as the app wires them.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly PersonaStore personas;

        /// <summary>Builds the stores and seeds one Platform Task holding <paramref name="seedRank"/>.</summary>
        /// <param name="seedRank">The rank the seeded Task holds, or <see langword="null"/> for none.</param>
        public Harness(int? seedRank)
        {
            _ = TestTaskStore.WriteTask(
                TestTaskStore.Root(this.dir),
                TestTaskStore.RelativePath("Platform", null, false, "PLAT-0050.md"),
                TestTasks.Make(id: "PLAT-0050", creator: "You", location: new("Platform", null, false)) with { Rank = seedRank });

            ManualTimeProvider clock = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
            this.personas = this.dir.CreatePersonaStore();
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
