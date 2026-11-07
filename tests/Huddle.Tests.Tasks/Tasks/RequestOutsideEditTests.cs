using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// An outside edit of a Request Task meets the same two lifecycle refusals as the tools (ADR-0040): it cannot leave
/// Backlog without triage and it cannot close unconfirmed. The edit is written to disk as an Agent's Bash or Write would,
/// then reported through <see cref="TaskStore.RebuildFromWatcher"/>.
/// </summary>
public sealed class RequestOutsideEditTests
{
    private const string LeaveBacklogRefusal = "A Request leaves Backlog only when its receiving manager accepts or declines it with triage_request.";
    private const string CloseRefusal = "A Request closes only after the requesting Team's verifier confirms it with confirm_request, and that needs it in Done. Done is not closed: if it isn't Done yet, move it to Done with update_task, then ask them to confirm.";

    /// <summary>Request frontmatter filed by Design's Dana.</summary>
    private static List<KeyValuePair<string, string>> Pairs(TriageState triage = TriageState.None, string confirmedBy = "") =>
        [.. new RequestFields(RequestKind.Bug, "Design", "Dana", triage, string.Empty, confirmedBy).ToFrontmatter()];

    /// <summary>A Backlog Request with no triage, edited by hand to To Do, is put back in Backlog with the body kept and a refusal entry.</summary>
    [Fact]
    public void OutsideEdit_RequestLeavesBacklogToToDo_IsReverted()
    {
        using Fixture fixture = new(TaskState.Backlog, Pairs());

        fixture.EditInPlace(fixture.Original with { Status = TaskState.ToDo });

        TaskItem after = fixture.Current();
        Assert.Equal(TaskState.Backlog, after.Status);
        Assert.Equal("Body", after.Description);
        Assert.Equal("outside edit refused: " + LeaveBacklogRefusal, after.ChangeLog[^1].Summary);
        Assert.Contains("status: Backlog", File.ReadAllText(fixture.OpenPath), StringComparison.Ordinal); // contains-ok: one frontmatter line in the whole file; the parsed status is asserted exactly above
        _ = Assert.Single(fixture.Changes);
    }

    /// <summary>A Backlog Request with no triage, edited by hand to In Progress, is put back in Backlog the same way.</summary>
    [Fact]
    public void OutsideEdit_RequestLeavesBacklogToInProgress_IsReverted()
    {
        using Fixture fixture = new(TaskState.Backlog, Pairs());

        fixture.EditInPlace(fixture.Original with { Status = TaskState.InProgress });

        TaskItem after = fixture.Current();
        Assert.Equal(TaskState.Backlog, after.Status);
        Assert.Equal("Body", after.Description);
        Assert.Equal("outside edit refused: " + LeaveBacklogRefusal, after.ChangeLog[^1].Summary);
        Assert.Contains("status: Backlog", File.ReadAllText(fixture.OpenPath), StringComparison.Ordinal); // contains-ok: one frontmatter line in the whole file; the parsed status is asserted exactly above
        _ = Assert.Single(fixture.Changes);
    }

    /// <summary>A Request moved to To Do with the accepted triage that triage_request writes is not reverted.</summary>
    [Fact]
    public void OutsideEdit_RequestToDoWithAcceptedTriage_IsNotReverted()
    {
        using Fixture fixture = new(TaskState.Backlog, Pairs());

        fixture.EditInPlace(fixture.Original with { Status = TaskState.ToDo, UnknownFields = Pairs(TriageState.Accepted) });

        TaskItem after = fixture.Current();
        Assert.Equal(TaskState.ToDo, after.Status);
        Assert.StartsWith("edited outside Huddle: ", after.ChangeLog[^1].Summary, StringComparison.Ordinal); // contains-ok: proves the ordinary log, not a refusal; the diff wording is OutsideEditLoggingTests' to pin
    }

    /// <summary>A Request moved to Rejected with the declined triage is not reverted.</summary>
    [Fact]
    public void OutsideEdit_RequestRejectedWithDeclinedTriage_IsNotReverted()
    {
        using Fixture fixture = new(TaskState.Backlog, Pairs());

        fixture.EditInPlace(fixture.Original with { Status = TaskState.Rejected, UnknownFields = Pairs(TriageState.Declined) });

        TaskItem after = fixture.Current();
        Assert.Equal(TaskState.Rejected, after.Status);
        Assert.StartsWith("edited outside Huddle: ", after.ChangeLog[^1].Summary, StringComparison.Ordinal); // contains-ok: proves the ordinary log, not a refusal; the diff wording is OutsideEditLoggingTests' to pin
    }

    /// <summary>An ordinary Task (no request key) moved from Backlog to To Do by hand is not reverted.</summary>
    [Fact]
    public void OutsideEdit_OrdinaryTaskLeavesBacklog_IsNotReverted()
    {
        using Fixture fixture = new(TaskState.Backlog, []);

        fixture.EditInPlace(fixture.Original with { Status = TaskState.ToDo });

        TaskItem after = fixture.Current();
        Assert.Equal(TaskState.ToDo, after.Status);
        Assert.StartsWith("edited outside Huddle: ", after.ChangeLog[^1].Summary, StringComparison.Ordinal); // contains-ok: proves the ordinary log, not a refusal; the diff wording is OutsideEditLoggingTests' to pin
    }

    /// <summary>An unconfirmed Request in Done whose file is moved into _closed/ is moved back and the refusal logged.</summary>
    [Fact]
    public void OutsideEdit_UnconfirmedRequestMovedToClosed_IsMovedBack()
    {
        using Fixture fixture = new(TaskState.Done, Pairs(TriageState.Accepted));

        fixture.MoveToClosed();

        TaskItem after = fixture.Current();
        Assert.False(after.Location.Closed);
        Assert.True(File.Exists(fixture.OpenPath));
        Assert.False(File.Exists(fixture.ClosedPath));
        Assert.Equal("outside edit refused: " + CloseRefusal, after.ChangeLog[^1].Summary);
    }

    /// <summary>One write that both closes a Backlog Request and moves it to To Do meets both rules: it is moved back, then put back in Backlog.</summary>
    [Fact]
    public void OutsideEdit_RequestClosedAndLeavingBacklogInOneWrite_BothReverted()
    {
        using Fixture fixture = new(TaskState.Backlog, Pairs());

        fixture.MoveToClosedAs(fixture.Original with { Status = TaskState.ToDo });

        TaskItem after = fixture.Current();
        Assert.False(after.Location.Closed);
        Assert.Equal(TaskState.Backlog, after.Status);
        Assert.Equal(
            ["outside edit refused: " + CloseRefusal, "outside edit refused: " + LeaveBacklogRefusal],
            after.ChangeLog.TakeLast(2).Select(static entry => entry.Summary).ToArray());
    }

    /// <summary>A Request confirmed by the requesting Team's verifier moved into _closed/ stays closed.</summary>
    [Fact]
    public void OutsideEdit_ConfirmedRequestMovedToClosed_StaysClosed()
    {
        using Fixture fixture = new(TaskState.Done, Pairs(TriageState.Accepted, "Quinn"));

        fixture.MoveToClosed();

        Assert.True(fixture.Current().Location.Closed);
        Assert.True(File.Exists(fixture.ClosedPath));
        Assert.False(File.Exists(fixture.OpenPath));
    }

    /// <summary>A declined Request has nothing to confirm, so moving it into _closed/ stays closed.</summary>
    [Fact]
    public void OutsideEdit_DeclinedRequestMovedToClosed_StaysClosed()
    {
        using Fixture fixture = new(TaskState.Rejected, Pairs(TriageState.Declined));

        fixture.MoveToClosed();

        Assert.True(fixture.Current().Location.Closed);
        Assert.True(File.Exists(fixture.ClosedPath));
        Assert.False(File.Exists(fixture.OpenPath));
    }

    /// <summary>An ordinary Task moved into _closed/ stays closed (today's behaviour, out of scope).</summary>
    [Fact]
    public void OutsideEdit_OrdinaryTaskMovedToClosed_StaysClosed()
    {
        using Fixture fixture = new(TaskState.Done, []);

        fixture.MoveToClosed();

        Assert.True(fixture.Current().Location.Closed);
        Assert.True(File.Exists(fixture.ClosedPath));
        Assert.False(File.Exists(fixture.OpenPath));
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

        /// <summary>Rewrites the file where it stands, as an Agent's Write would, and reports it.</summary>
        /// <param name="edited">The Task the file should now read as.</param>
        public void EditInPlace(TaskItem edited)
        {
            File.WriteAllText(this.OpenPath, TaskFileFormat.Compose(edited));
            this.store.RebuildFromWatcher();
        }

        /// <summary>Moves the file into <c>_tasks/_closed/</c>, as an Agent's Bash <c>mv</c> would, and reports it.</summary>
        public void MoveToClosed()
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(this.ClosedPath) ?? throw new InvalidOperationException("no closed folder"));
            File.Move(this.OpenPath, this.ClosedPath);
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
