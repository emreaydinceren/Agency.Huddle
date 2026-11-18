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
using ProofContextBuilder = Agency.Huddle.App.Tasks.HumanGate.ProofContextBuilder;
using ProofInputWatcher = Agency.Huddle.App.Tasks.HumanGate.ProofInputWatcher;
using ProofItem = Agency.Huddle.App.Tasks.HumanGate.ProofItem;
using ProofSource = Agency.Huddle.App.Tasks.HumanGate.ProofSource;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// A review is a Task (#669, D-8, D-16): the Definition of Done line <c>Review: task tagged review</c> is met only when a child tagged
/// <c>review</c> is Done, a comment with findings leaves the child open, a gated In Progress parent then moves to Needs You as Huddle,
/// and the <c>QA evidence</c> item is <c>task tagged verify</c>.
/// </summary>
public sealed class ReviewTaskProofTests
{
    private const string ReviewProcess = """
        # Flow

        ## Definition of Done

        - Review: task tagged review
        """;

    private const string VerifyProcess = """
        # Flow

        ## Definition of Done

        - Review: task tagged review
        - QA: task tagged verify
        """;

    private const string NoGates = "[]";
    private const string QaGateList = "['qa-evidence-before-done']";

    private static readonly DateTimeOffset Noon = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A comment with findings leaves the review Task open and the <c>Review</c> item unmet, with the box count in its detail.</summary>
    [Fact]
    public void Comment_WithFindings_LeavesReviewTaskOpenAndItemUnmet()
    {
        using Rig rig = new(ReviewProcess, NoGates);
        TaskItem parent = rig.GatedParent();
        TaskItem review = rig.CreateChild(parent, "review");

        rig.Comment(review, "Findings: the login accepts an empty password.");

        Assert.Equal(TaskState.InProgress, rig.Read(review).Status);
        ProofItem item = Assert.Single(rig.ProofOf(parent).Items);
        Assert.False(item.Met);
        Assert.Equal($"0 of 0 in #{review.Id}", item.Detail);
        Assert.Equal(TaskState.InProgress, rig.Read(parent).Status);
    }

    /// <summary>The <c>Review</c> item is met only when the review Task is Done, and the gated In Progress parent then moves to Needs You as Huddle, waking nobody.</summary>
    [Fact]
    public void ReviewTaskDone_MeetsItemAndMovesGatedParentToNeedsYouAsHuddle()
    {
        using Rig rig = new(ReviewProcess, NoGates);
        TaskItem parent = rig.GatedParent();
        TaskItem review = rig.CreateChild(parent, "review");
        rig.Block(parent, review);
        rig.Comment(review, "Findings: the login accepts an empty password.");
        Assert.False(Assert.Single(rig.ProofOf(parent).Items).Met);
        List<TaskChange> changes = [];
        rig.Events.TaskChanged += changes.Add;

        rig.MarkDone(review);

        Assert.True(Assert.Single(rig.ProofOf(parent).Items).Met);
        TaskItem after = rig.Read(parent);
        Assert.Equal(TaskState.NeedsYou, after.Status);
        Assert.Equal("Huddle", after.ChangeLog[^1].Actor);
        TaskChange move = Assert.Single(changes, change => change.After.Id == parent.Id);
        Assert.True(TaskActors.IsReadyMove(move));
    }

    /// <summary>The <c>QA evidence</c> item is <c>task tagged verify</c>: a Done child tagged verify meets it.</summary>
    [Fact]
    public void QaEvidenceItem_DoneVerifyChild_IsMet()
    {
        using Rig rig = new(ReviewProcess, QaGateList);
        TaskItem parent = rig.GatedParent();
        TaskItem verify = rig.CreateChild(parent, "verify");
        rig.MarkDone(verify);

        ProofItem item = Assert.Single(rig.ProofOf(parent).Items, static candidate => candidate.Label == "QA evidence");

        Assert.True(item.Met);
    }

    /// <summary>An Evidence line from a member who verifies no longer meets the <c>QA evidence</c> item on its own; it says there is no Task yet.</summary>
    [Fact]
    public void QaEvidenceItem_VerifierEvidenceAlone_IsUnmet()
    {
        using Rig rig = new(ReviewProcess, QaGateList);
        TaskItem parent = rig.GatedParent();
        rig.AddEvidence(parent);

        ProofItem item = Assert.Single(rig.ProofOf(parent).Items, static candidate => candidate.Label == "QA evidence");

        Assert.False(item.Met);
        Assert.Equal("no Task yet", item.Detail);
    }

    /// <summary>A Definition of Done that already says <c>task tagged verify</c> counts as naming QA evidence, so the gate adds no second item.</summary>
    [Fact]
    public void QaEvidenceItem_DefinitionNamesTaskTaggedVerify_IsNotAddedAgain()
    {
        using Rig rig = new(VerifyProcess, QaGateList);
        TaskItem parent = rig.GatedParent();

        IReadOnlyList<ProofItem> items = rig.ProofOf(parent).Items;

        Assert.Equal(["Review", "QA"], items.Select(static item => item.Label).ToArray());
    }

    /// <summary>Hands the Task service a Proof source built after it, since the source reads the service's own store.</summary>
    private sealed class DeferredProofSource : IProofSource
    {
        /// <summary>The real source, set once the stores exist.</summary>
        public ProofSource? Inner { get; set; }

        /// <inheritdoc/>
        public Proof For(TaskItem task) => this.Inner?.For(task) ?? Proof.Empty;
    }

    /// <summary>Real stores: a Task harness whose Proof is the real source over a Team whose Definition of Done and gates the test chooses.</summary>
    private sealed class Rig : IDisposable
    {
        private const string Team = TaskGateText.Team;

        private static readonly TaskActor Quinn = new(TaskActorKind.Agent, "Quinn", "quinn-id");

        private readonly TempDataDir dir = new();
        private readonly TaskToolHarness harness;
        private readonly LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        private readonly DocHistoryStore history;
        private readonly ProofSource source;
        private readonly ProofInputWatcher watcher;

        /// <summary>Seeds the Agents, the Process and the stores.</summary>
        /// <param name="process">The Process document text, whose Definition of Done is the Proof.</param>
        /// <param name="gates">The gates list as it is written in <c>_team.md</c>, brackets included.</param>
        public Rig(string process, string gates)
        {
            DeferredProofSource proof = new();
            this.harness = new TaskToolHarness(clock: new ManualTimeProvider(Noon.AddHours(1)), proof: proof);
            _ = this.harness.Personas.Add(new PersonaIdentity("Lead", "Lead", "lead", [Team], Role: new RoleRef("web", "lead", "1.2.0")), "You lead.");
            _ = this.harness.Personas.Add(new PersonaIdentity("Quinn", "QA Engineer", "quinn", [Team], Role: new RoleRef("web", "qa", "1.2.0")), "You verify.");
            this.history = new DocHistoryStore(this.library.BoundOptions, NullLogger<DocHistoryStore>.Instance);
            File.WriteAllText(Path.Combine(this.dir.Path, "AGENTS.md"), process);
            string teamFile = $"---\nteamType: 'web@1.2.0'\ngates: {gates}\ncheckCommand: 'dotnet test'\n---\n";
            ProcessGateService processGates = TaskGateText.BuildGates(this.dir, teamFile);
            FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], ["Lead", "Quinn"], true) { TeamType = new VersionedRef("web", "1.2.0") }] };
            this.source = new ProofSource(new ProofContextBuilder(this.harness.Store, this.harness.Personas, processGates, catalog, this.library.Resolver, this.history));
            proof.Inner = this.source;
            this.watcher = new ProofInputWatcher(this.history, this.harness.Events, this.harness.Store, this.harness.Service, this.library.Resolver);
        }

        /// <summary>The Task events, to see the changes a step raises.</summary>
        public TaskEvents Events => this.harness.Events;

        /// <summary>Creates a Human-gated Task In Progress as the Human, so creation itself moves nothing.</summary>
        public TaskItem GatedParent()
        {
            TaskDraft draft = new("Write the reply", Team, null, TaskState.InProgress) { HumanGate = true };
            return Assert.IsType<TaskResult.Saved>(this.harness.Service.Create(draft, this.Human)).Task;
        }

        /// <summary>Creates an open child of <paramref name="parent"/> tagged <paramref name="tag"/>, assigned to Quinn, as the Human.</summary>
        /// <param name="parent">The parent Task.</param>
        /// <param name="tag">The tag.</param>
        public TaskItem CreateChild(TaskItem parent, string tag)
        {
            TaskDraft draft = new("Check the reply", Team, null, TaskState.InProgress, Assignee: "Quinn", Parent: parent.Id, Tags: [tag]);
            return Assert.IsType<TaskResult.Saved>(this.harness.Service.Create(draft, this.Human)).Task;
        }

        /// <summary>Adds <paramref name="blocker"/> to the parent's <c>blocked_by</c> as the Human, which never moves a Task.</summary>
        /// <param name="parent">The blocked Task.</param>
        /// <param name="blocker">The Task it waits for.</param>
        public void Block(TaskItem parent, TaskItem blocker)
        {
            _ = Assert.IsType<TaskResult.Saved>(this.harness.Service.Update(parent.Id, new TaskPatch { BlockedBy = [blocker.Id] }, null, this.Human));
        }

        /// <summary>Comments on a Task as Quinn.</summary>
        /// <param name="task">The Task.</param>
        /// <param name="text">The comment.</param>
        public void Comment(TaskItem task, string text)
        {
            _ = Assert.IsType<TaskResult.Saved>(this.harness.Service.AddComment(task.Id, text, Quinn));
        }

        /// <summary>Moves a Task to Done as Quinn.</summary>
        /// <param name="task">The Task.</param>
        public void MarkDone(TaskItem task)
        {
            _ = Assert.IsType<TaskResult.Saved>(this.harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, Quinn));
        }

        /// <summary>Adds an Evidence note by Quinn.</summary>
        /// <param name="task">The Task.</param>
        public void AddEvidence(TaskItem task)
        {
            EvidenceLine line = new(Noon, "Quinn", string.Empty, null, null, null, "Read the reply against the brief.");
            _ = Assert.IsType<TaskResult.Saved>(this.harness.Service.AddEvidence(task.Id, line, Quinn));
        }

        /// <summary>The Proof of a Task as the real source computes it now.</summary>
        /// <param name="task">The Task.</param>
        public Proof ProofOf(TaskItem task) => this.source.For(this.Read(task));

        /// <summary>Reads the Task as stored now.</summary>
        /// <param name="task">The Task.</param>
        public TaskItem Read(TaskItem task) => this.harness.Store.Get(task.Id) ?? throw new InvalidOperationException("The Task is missing.");

        /// <summary>Disposes the watcher, the stores and temp folders.</summary>
        public void Dispose()
        {
            this.watcher.Dispose();
            this.harness.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }

        private TaskActor Human => TaskActors.Human(this.harness.Options.Value);
    }
}
