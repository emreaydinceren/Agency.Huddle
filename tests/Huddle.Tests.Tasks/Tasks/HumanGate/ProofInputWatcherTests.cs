using Agency.Huddle.App.Acp;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using IProofSource = Agency.Huddle.App.Tasks.HumanGate.IProofSource;
using Proof = Agency.Huddle.App.Tasks.HumanGate.Proof;
using ProofInputWatcher = Agency.Huddle.App.Tasks.HumanGate.ProofInputWatcher;
using ProofContextBuilder =Agency.Huddle.App.Tasks.HumanGate.ProofContextBuilder;
using ProofSource = Agency.Huddle.App.Tasks.HumanGate.ProofSource;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// A Proof input that lives outside the Task file can complete a gated Task's Proof with no write to the Task (#593). Huddle then
/// moves the Task from Review to Needs You by itself, the same way and with the same Change log line as after a Task write.
/// </summary>
public sealed class ProofInputWatcherTests
{
    private const string TeamFileText = """
        ---
        teamType: 'web@1.2.0'
        gates: []
        checkCommand: 'dotnet test'
        ---
        """;

    private const string Process = """
        # Flow

        ## Definition of Done

        - Checked: evidence from verify
        - Draft reviewed: document reviewed by verify
        """;

    private const string DocumentOnly = """
        # Flow

        ## Definition of Done

        - Draft reviewed: document reviewed by verify
        """;

    private const string ChildOnly = """
        # Flow

        ## Definition of Done

        - Citations: task tagged citation-check
        """;

    private static readonly DateTimeOffset Noon = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The #550 live run: the attached draft was edited after Quinn's review, so Proof stayed at 1/2 when the Evidence came in. Quinn's
    /// re-review completes Proof, and the gated Task moves to Needs You with the Proof count, as Huddle.
    /// </summary>
    [Fact]
    public void ReReview_CompletesProofOfGatedTaskInReview_MovesToNeedsYou()
    {
        using Rig rig = new();
        TaskItem task = rig.GatedTaskInReview(rig.Attach("draft.md"));
        rig.Log("draft.md", Entry(0, "Lead", DocChangeKind.Created), Entry(1, "Quinn", DocChangeKind.Reviewed), Entry(2, "Lead", DocChangeKind.Edited));
        rig.AddEvidence(task);
        Assert.Equal(TaskState.InProgress, rig.Read(task).Status);

        rig.Log("draft.md", Entry(3, "Quinn", DocChangeKind.Reviewed));

        TaskItem after = rig.Read(task);
        Assert.Equal(TaskState.NeedsYou, after.Status);
        ChangeLogEntry last = after.ChangeLog[^1];
        Assert.Equal("status: In Progress → Needs You (Proof 2/2)", last.Summary);
        Assert.Equal("Huddle", last.Actor);
    }

    /// <summary>The move is one change by Huddle, and a later review of an already moved Task changes nothing.</summary>
    [Fact]
    public void ReReview_RaisesExactlyOneChangeByHuddle()
    {
        using Rig rig = new();
        TaskItem task = rig.GatedTaskInReview(rig.Attach("draft.md"));
        rig.Log("draft.md", Entry(0, "Lead", DocChangeKind.Created), Entry(1, "Quinn", DocChangeKind.Reviewed), Entry(2, "Lead", DocChangeKind.Edited));
        rig.AddEvidence(task);
        List<TaskChange> changes = [];
        rig.Events.TaskChanged += changes.Add;

        rig.Log("draft.md", Entry(3, "Quinn", DocChangeKind.Reviewed));
        rig.Log("draft.md", Entry(4, "Quinn", DocChangeKind.Reviewed));

        TaskChange change = Assert.Single(changes);
        Assert.Equal(TaskActorKind.Helper, change.Actor.Kind);
        Assert.Equal(TaskState.InProgress, change.Before?.Status);
        Assert.Equal(TaskState.NeedsYou, change.After.Status);
        Assert.Equal(TaskState.NeedsYou, rig.Read(task).Status);
    }

    /// <summary>An ungated Task in Review stays there however fresh the review is: it goes to Done, never to Needs You.</summary>
    [Fact]
    public void ReReview_UngatedTask_StaysInReview()
    {
        using Rig rig = new();
        TaskItem task = rig.GatedTaskInReview(rig.Attach("draft.md"), gated: false);
        rig.AddEvidence(task);
        List<TaskChange> changes = [];
        rig.Events.TaskChanged += changes.Add;

        rig.Log("draft.md", Entry(0, "Lead", DocChangeKind.Created), Entry(1, "Quinn", DocChangeKind.Reviewed));

        Assert.Equal(TaskState.InProgress, rig.Read(task).Status);
        Assert.Empty(changes);
    }

    /// <summary>A review of a document the Task does not attach re-evaluates nothing; a review of the attached one does.</summary>
    [Fact]
    public void Review_OfAnotherDocument_DoesNotMoveTheTask()
    {
        using Rig rig = new(DocumentOnly);
        TaskItem task = rig.GatedTaskInReview(rig.Attach("draft.md"));
        _ = rig.Attach("other.md");
        rig.LogQuietly("draft.md", Entry(0, "Lead", DocChangeKind.Created), Entry(1, "Quinn", DocChangeKind.Reviewed));
        List<TaskChange> changes = [];
        rig.Events.TaskChanged += changes.Add;

        rig.Log("other.md", Entry(2, "Quinn", DocChangeKind.Reviewed));

        Assert.Equal(TaskState.InProgress, rig.Read(task).Status);
        Assert.Empty(changes);

        rig.Log("draft.md", Entry(3, "Quinn", DocChangeKind.Reviewed));

        Assert.Equal(TaskState.NeedsYou, rig.Read(task).Status);
    }

    /// <summary>A Human's save as the newest entry never moves a Task, even when another attached document keeps the Proof complete.</summary>
    [Fact]
    public void HumanEdit_AsNewestEntry_DoesNotMoveTheTask()
    {
        using Rig rig = new(DocumentOnly);
        string description = rig.Attach("a.md") + "\n" + rig.Attach("b.md");
        TaskItem task = rig.GatedTaskInReview(description);
        rig.LogQuietly("a.md", Entry(0, "Lead", DocChangeKind.Created), Entry(1, "Quinn", DocChangeKind.Reviewed));
        List<TaskChange> changes = [];
        rig.Events.TaskChanged += changes.Add;

        rig.Log("b.md", new DocHistoryEntry(Noon.AddMinutes(2), "Human", DocActorKind.Human, DocChangeKind.Edited, "s"));

        Assert.Equal(TaskState.InProgress, rig.Read(task).Status);
        Assert.Empty(changes);

        rig.Log("a.md", Entry(3, "Quinn", DocChangeKind.Reviewed));

        Assert.Equal(TaskState.NeedsYou, rig.Read(task).Status);
    }

    /// <summary>A tagged child reaching Done completes its parent's Proof, and the parent moves to Needs You.</summary>
    [Fact]
    public void ChildDone_CompletesProofOfParentInReview_MovesToNeedsYou()
    {
        using Rig rig = new(ChildOnly);
        TaskItem parent = rig.GatedTaskInReview(string.Empty);
        TaskItem child = rig.CreateChild(parent, "citation-check");
        Assert.Equal(TaskState.InProgress, rig.Read(parent).Status);

        rig.MarkDone(child);

        TaskItem after = rig.Read(parent);
        Assert.Equal(TaskState.NeedsYou, after.Status);
        Assert.Equal("status: In Progress → Needs You (Proof 1/1)", after.ChangeLog[^1].Summary);
        Assert.Equal("Huddle", after.ChangeLog[^1].Actor);
    }

    /// <summary>One history entry by an Agent.</summary>
    /// <param name="minutes">Minutes after noon.</param>
    /// <param name="actor">Who made it.</param>
    /// <param name="kind">What kind of change.</param>
    private static DocHistoryEntry Entry(int minutes, string actor, DocChangeKind kind) =>
        new(Noon.AddMinutes(minutes), actor, DocActorKind.Agent, kind, "s");

    /// <summary>Hands the Task service a Proof source built after it, since the source reads the service's own store.</summary>
    private sealed class DeferredProofSource : IProofSource
    {
        /// <summary>The real source, set once the stores exist.</summary>
        public ProofSource? Inner { get; set; }

        /// <inheritdoc/>
        public Proof For(TaskItem task) => this.Inner?.For(task) ?? Proof.Empty;
    }

    /// <summary>Real stores: a Task harness whose Proof is the real source, two role-bearing Agents, a Library root and a document history store.</summary>
    private sealed class Rig : IDisposable
    {
        private const string Team = TaskGateText.Team;

        private static readonly TaskActor Quinn = new(TaskActorKind.Agent, "Quinn", "quinn-id");

        private readonly TempDataDir dir = new();
        private readonly TaskToolHarness harness;
        private readonly LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        private readonly string docsRoot;
        private readonly DocHistoryStore history;
        private readonly DocHistoryStore quietHistory;
        private readonly ProofInputWatcher watcher;

        /// <summary>Seeds the Agents, the Process and the stores.</summary>
        /// <param name="process">The Process document text, whose Definition of Done is the Proof.</param>
        public Rig(string process = Process)
        {
            DeferredProofSource proof = new();
            this.harness = new TaskToolHarness(clock: new ManualTimeProvider(Noon.AddHours(1)), proof: proof);
            _ = this.harness.Personas.Add(new PersonaIdentity("Lead", "Lead", "lead", [Team], Role: new RoleRef("web", "lead", "1.2.0")), "You lead.");
            _ = this.harness.Personas.Add(new PersonaIdentity("Quinn", "QA Engineer", "quinn", [Team], Role: new RoleRef("web", "qa", "1.2.0")), "You verify.");
            this.docsRoot = this.library.CreatePinnedRoot("Docs");
            this.history = new DocHistoryStore(this.library.BoundOptions, NullLogger<DocHistoryStore>.Instance);
            this.quietHistory = new DocHistoryStore(this.library.BoundOptions, NullLogger<DocHistoryStore>.Instance);
            File.WriteAllText(Path.Combine(this.dir.Path, "AGENTS.md"), process);
            ProcessGateService gates = TaskGateText.BuildGates(this.dir, TeamFileText);
            FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], ["Lead", "Quinn"], true) { TeamType = new VersionedRef("web", "1.2.0") }] };
            proof.Inner = new ProofSource(new ProofContextBuilder(this.harness.Store, this.harness.Personas, gates, catalog, this.library.Resolver, this.history));
            this.watcher = new ProofInputWatcher(this.history, this.harness.Events, this.harness.Store, this.harness.Service, this.library.Resolver);
        }

        /// <summary>The Task events, to count the changes a step raises.</summary>
        public TaskEvents Events => this.harness.Events;

        /// <summary>The Human's actor.</summary>
        public TaskActor Human => TaskActors.Human(this.harness.Options.Value);

        /// <summary>Creates a child Task of <paramref name="parent"/> carrying <paramref name="tag"/>, in Review, as the Human.</summary>
        /// <param name="parent">The parent Task.</param>
        /// <param name="tag">The tag.</param>
        public TaskItem CreateChild(TaskItem parent, string tag)
        {
            TaskDraft draft = new("Check the citations", Team, null, TaskState.InProgress, Parent: parent.Id, Tags: [tag]);
            return Assert.IsType<TaskResult.Saved>(this.harness.Service.Create(draft, this.Human)).Task;
        }

        /// <summary>Moves a Task to Done as Quinn.</summary>
        /// <param name="task">The Task.</param>
        public void MarkDone(TaskItem task)
        {
            _ = Assert.IsType<TaskResult.Saved>(this.harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, Quinn));
        }

        /// <summary>Writes a file in the Library root and returns the description line that attaches it.</summary>
        /// <param name="fileName">The file name.</param>
        public string Attach(string fileName)
        {
            File.WriteAllText(Path.Combine(this.docsRoot, fileName), "# Draft");
            return $"`{Path.Combine(this.docsRoot, fileName)}`";
        }

        /// <summary>Creates a Task as the Human (Human-gated and in Review by default), so creation itself moves nothing.</summary>
        /// <param name="description">The Task's description.</param>
        /// <param name="status">The status to create it in.</param>
        /// <param name="gated">Whether it is Human-gated.</param>
        public TaskItem GatedTaskInReview(string description, TaskState status = TaskState.InProgress, bool gated = true)
        {
            TaskDraft draft = new("Write the reply", Team, null, status, Description: description) { HumanGate = gated };
            return Assert.IsType<TaskResult.Saved>(this.harness.Service.Create(draft, TaskActors.Human(this.harness.Options.Value))).Task;
        }

        /// <summary>Adds an Evidence note by Quinn.</summary>
        /// <param name="task">The Task.</param>
        public void AddEvidence(TaskItem task)
        {
            EvidenceLine line = new(Noon, "Quinn", string.Empty, null, null, null, "Read the draft against the brief.");
            _ = Assert.IsType<TaskResult.Saved>(this.harness.Service.AddEvidence(task.Id, line, Quinn));
        }

        /// <summary>Reads the Task as stored now.</summary>
        /// <param name="task">The Task.</param>
        public TaskItem Read(TaskItem task) => this.harness.Store.Get(task.Id) ?? throw new InvalidOperationException("The Task is missing.");

        /// <summary>Appends <paramref name="entries"/> to the history of a file in the Library root.</summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="entries">The entries, oldest first.</param>
        public void Log(string fileName, params DocHistoryEntry[] entries)
        {
            foreach (DocHistoryEntry entry in entries)
            {
                this.history.Append(this.library.Resolve(this.docsRoot, fileName), entry);
            }
        }

        /// <summary>
        /// Appends entries through a second store over the same files, which the watcher does not listen to: the History changes with no
        /// event, so a Task whose Proof is complete by it has not been re-evaluated.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="entries">The entries, oldest first.</param>
        public void LogQuietly(string fileName, params DocHistoryEntry[] entries)
        {
            foreach (DocHistoryEntry entry in entries)
            {
                this.quietHistory.Append(this.library.Resolve(this.docsRoot, fileName), entry);
            }
        }

        /// <summary>Disposes the watcher, the stores and temp folders.</summary>
        public void Dispose()
        {
            this.watcher.Dispose();
            this.harness.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }
    }
}
