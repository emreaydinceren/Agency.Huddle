using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// An outside move into or out of <c>_tasks/_closed/</c> is logged like any other outside edit: the entry ends in
/// <c>closed</c> or <c>reopened</c>, after the field summary when the same write also changed fields. The edit is written to
/// disk as an Agent's Bash or Write would, then reported through <see cref="TaskStore.RebuildFromWatcher"/>.
/// </summary>
public sealed class OutsideCloseLogTests
{
    /// <summary>Request frontmatter filed by Design's Dana.</summary>
    private static List<KeyValuePair<string, string>> Pairs(TriageState triage = TriageState.None, string confirmedBy = "") =>
        [.. new RequestFields(RequestKind.Bug, "Design", "Dana", triage, string.Empty, confirmedBy).ToFrontmatter()];

    /// <summary>An ordinary Task in Done moved into _closed/ stays closed and logs exactly "edited outside Huddle: closed", raising one change.</summary>
    [Fact]
    public void OutsideEdit_OrdinaryTaskMovedToClosed_LogsClosed()
    {
        using Fixture fixture = new(TaskState.Done, []);

        fixture.MoveToClosed();

        TaskItem after = fixture.Current();
        Assert.Equal("edited outside Huddle: closed", after.ChangeLog[^1].Summary);
        Assert.True(after.Location.Closed);
        _ = Assert.Single(fixture.Changes);
    }

    /// <summary>A closed ordinary Task moved back out of _closed/ logs exactly "edited outside Huddle: reopened".</summary>
    [Fact]
    public void OutsideEdit_ClosedTaskMovedOutOfClosed_LogsReopened()
    {
        using Fixture fixture = new(TaskState.Done, []);
        fixture.MoveToClosed();

        fixture.MoveOutOfClosed();

        TaskItem after = fixture.Current();
        Assert.Equal("edited outside Huddle: reopened", after.ChangeLog[^1].Summary);
        Assert.False(after.Location.Closed);
    }

    /// <summary>A write that moves a Task into _closed/ and changes its priority logs the field summary followed by "; closed".</summary>
    [Fact]
    public void OutsideEdit_ClosedAndPriorityChangedInOneWrite_LogsFieldSummaryThenClosed()
    {
        using Fixture fixture = new(TaskState.Done, []);
        TaskPriority changed = fixture.Original.Priority == TaskPriority.Urgent ? TaskPriority.Low : TaskPriority.Urgent;

        fixture.MoveToClosedAs(fixture.Original with { Priority = changed });

        TaskItem after = fixture.Current();
        Assert.Equal(
            "edited outside Huddle: priority: " + fixture.Original.Priority.ToWire() + " → " + changed.ToWire() + "; closed",
            after.ChangeLog[^1].Summary);
        Assert.True(after.Location.Closed);
    }

    /// <summary>A confirmed Request moved into _closed/ stays closed and now also logs "edited outside Huddle: closed".</summary>
    [Fact]
    public void OutsideEdit_ConfirmedRequestMovedToClosed_LogsClosed()
    {
        using Fixture fixture = new(TaskState.Done, Pairs(TriageState.Accepted, "Quinn"));

        fixture.MoveToClosed();

        TaskItem after = fixture.Current();
        Assert.Equal("edited outside Huddle: closed", after.ChangeLog[^1].Summary);
        Assert.True(after.Location.Closed);
    }

    /// <summary>A store with one Platform Task on disk and a <see cref="TaskService"/> watching it.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly PersonaStore personas;
        private readonly TaskStore store;
        private readonly TaskService service;
        private readonly TaskId id;

        /// <summary>Writes the Task to disk and starts the service.</summary>
        /// <param name="status">The Task's starting status.</param>
        /// <param name="unknownFields">The frontmatter keys beyond the known ones (the Request keys, or none).</param>
        public Fixture(TaskState status, List<KeyValuePair<string, string>> unknownFields)
        {
            string root = TestTaskStore.Root(this.dir);
            this.OpenPath = TestTaskStore.WriteTask(
                root,
                Path.Combine("Platform", "_tasks", "PLAT-0001.md"),
                TestTasks.Make(id: "PLAT-0001", status: status, creator: "You", description: "Body", location: new("Platform", null, false), unknownFields: unknownFields));
            this.ClosedPath = Path.Combine(root, "Platform", "_tasks", "_closed", "PLAT-0001.md");
            this.personas = this.dir.CreatePersonaStore();
            this.store = TestTaskStore.CreateTaskStore(this.dir, this.personas);
            TaskEvents events = new();
            this.service = new(this.store, new TaskIdAllocator(this.dir.Options()), events, this.personas, new EmptySprintCatalog(), this.dir.Options(), TimeProvider.System, NullLogger<TaskService>.Instance);
            events.TaskChanged += this.Changes.Add;
            _ = TaskId.TryParse("PLAT-0001", out this.id);
            this.Original = this.Current();
        }

        /// <summary>The path of the Task file under <c>_tasks/</c>.</summary>
        public string OpenPath { get; }

        /// <summary>The path of the Task file under <c>_tasks/_closed/</c>.</summary>
        public string ClosedPath { get; }

        /// <summary>The Task as the store read it before the edit.</summary>
        public TaskItem Original { get; }

        /// <summary>Every <see cref="TaskEvents.TaskChanged"/> raised since the service started.</summary>
        public List<TaskChange> Changes { get; } = [];

        /// <summary>The Task as the store holds it now.</summary>
        public TaskItem Current() => this.store.Get(this.id) ?? throw new InvalidOperationException("fixture task missing");

        /// <summary>Moves the file into <c>_tasks/_closed/</c>, as an Agent's Bash <c>mv</c> would, and reports it.</summary>
        public void MoveToClosed()
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(this.ClosedPath) ?? throw new InvalidOperationException("no closed folder"));
            File.Move(this.OpenPath, this.ClosedPath);
            this.store.RebuildFromWatcher();
        }

        /// <summary>Moves the file from <c>_tasks/_closed/</c> back to <c>_tasks/</c>, as an Agent's Bash <c>mv</c> would, and reports it.</summary>
        public void MoveOutOfClosed()
        {
            File.Move(this.ClosedPath, this.OpenPath);
            this.store.RebuildFromWatcher();
        }

        /// <summary>Writes <paramref name="edited"/> into <c>_tasks/_closed/</c> and removes the open file, as one Agent write that edits and closes, and reports it.</summary>
        /// <param name="edited">The Task the closed file should read as.</param>
        public void MoveToClosedAs(TaskItem edited)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(this.ClosedPath) ?? throw new InvalidOperationException("no closed folder"));
            File.WriteAllText(this.ClosedPath, TaskFileFormat.Compose(edited));
            File.Delete(this.OpenPath);
            this.store.RebuildFromWatcher();
        }

        /// <summary>Releases the service, store and temp folder.</summary>
        public void Dispose()
        {
            this.service.Dispose();
            this.store.Dispose();
            this.personas.Dispose();
            this.dir.Dispose();
        }
    }
}
