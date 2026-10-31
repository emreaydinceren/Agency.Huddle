using Agency.Huddle.Tests.Factories;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests that the Human gate's decision lines reach the evidence bundle a retro starts from (Human gate Spec §6.11).</summary>
public sealed class RecordsBundleDecisionTests
{
    private static readonly DateTimeOffset Since = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A <c>changes requested:</c> Change log line since the cutoff reaches the bundle, so repeated reasons can be seen.</summary>
    [Fact]
    public void Build_ChangesRequestedLine_ReachesTheBundle()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.Write("PLAT-0001", "A", TaskState.InProgress, [
            TestTasks.Entry("2026-10-02T10:00:00Z", "You", "changes requested: the tests do not cover the retry"),
        ]);

        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        RecordEvent recorded = Assert.Single(bundle.Escalations);
        Assert.Equal("changes requested: the tests do not cover the retry", recorded.Summary);
        Assert.Equal("PLAT-0001", recorded.TaskId);
    }

    /// <summary>A <c>declined:</c> Change log line since the cutoff reaches the bundle; one before the cutoff does not.</summary>
    [Fact]
    public void Build_DeclinedLine_ReachesTheBundleOnlySinceTheCutoff()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.Write("PLAT-0001", "A", TaskState.Rejected, [
            TestTasks.Entry("2026-09-01T10:00:00Z", "You", "declined: out of date"),
            TestTasks.Entry("2026-10-03T10:00:00Z", "You", "declined: we are not building this"),
        ]);

        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        RecordEvent recorded = Assert.Single(bundle.Escalations);
        Assert.Equal("declined: we are not building this", recorded.Summary);
    }

    /// <summary>Builds a <see cref="RecordsBundleBuilder"/> over a temporary Task store.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private TaskStore? tasks;
        private RecordsBundleBuilder? builder;

        /// <summary>Creates the fixture over <paramref name="dir"/>.</summary>
        /// <param name="dir">The temporary data directory.</param>
        public Fixture(TempDataDir dir)
        {
            this.dir = dir;
            this.Spend = new PersonaSpendStore(dir.Options());
            this.Catalog.Teams = [new TeamSummary("Platform", [], ["Ada", "Bo"], true)];
        }

        /// <summary>Gets the fake Team catalog.</summary>
        public FakeTeamCatalog Catalog { get; } = new();

        /// <summary>Gets the spend store.</summary>
        public PersonaSpendStore Spend { get; }

        /// <summary>Gets the builder, created on first use so Tasks written before it are seen.</summary>
        public RecordsBundleBuilder Builder
        {
            get
            {
                if (this.builder is null)
                {
                    this.tasks = TestTaskStore.CreateTaskStore(this.dir, this.dir.CreatePersonaStore());
                    this.builder = new RecordsBundleBuilder(this.tasks, this.Catalog, this.Spend, this.dir.Options());
                }

                return this.builder;
            }
        }

        /// <summary>Writes one Platform Task with the given Change log.</summary>
        /// <param name="id">The Task id.</param>
        /// <param name="title">The title.</param>
        /// <param name="status">The status.</param>
        /// <param name="log">The Change log.</param>
        public void Write(string id, string title, TaskState status, IReadOnlyList<ChangeLogEntry> log)
        {
            TaskItem task = TestTasks.Make(id, title, status, changeLog: log, location: new("Platform", null, false));
            _ = TestTaskStore.WriteTask(TestTaskStore.Root(this.dir), TestTaskStore.RelativePath("Platform", null, false, id + ".md"), task);
        }

        /// <summary>Disposes the Task store.</summary>
        public void Dispose() => this.tasks?.Dispose();
    }
}
