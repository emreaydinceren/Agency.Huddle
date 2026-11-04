using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Proof = Agency.Huddle.App.Tasks.HumanGate.Proof;
using ProofContext = Agency.Huddle.App.Tasks.HumanGate.ProofContext;
using ProofDocument = Agency.Huddle.App.Tasks.HumanGate.ProofDocument;
using ProofEvaluator = Agency.Huddle.App.Tasks.HumanGate.ProofEvaluator;
using ProofItem = Agency.Huddle.App.Tasks.HumanGate.ProofItem;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// <c>ProofEvaluator.Evaluate</c> turns a Team's Definition of Done into a Proof, one item per rule, with the owner
/// and detail strings of Spec §6.3. It is pure: every input arrives in a <c>ProofContext</c>.
/// </summary>
public sealed class ProofEvaluatorTests
{
    private const string Verify = "verify";
    private const string Reviewed = "Reviewed by Ruth";
    private const string Quinn = "Quinn";
    private const string Ruth = "Ruth";
    private const string Dev = "Dev";

    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<string, string> Covers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Quinn] = "verify, review",
        [Ruth] = "review",
        [Dev] = "build",
    };

    private static readonly string[] Roster = [Dev, Quinn, Ruth];

    /// <summary>An Evidence line with a command, by a member whose role covers the step, meets <c>evidence from</c>.</summary>
    [Fact]
    public void EvidenceFrom_Met()
    {
        ProofContext context = Context([Item(new DoneRule.EvidenceFrom(Verify))], evidence: [Run(Quinn, "dotnet test", 0)]);

        ProofItem item = Single(context);

        Assert.True(item.Met);
        Assert.Null(item.Owner);
        Assert.Null(item.Detail);
    }

    /// <summary>With no qualifying Evidence the item is open, owned by the first member covering the step in roster order, with no detail.</summary>
    [Fact]
    public void EvidenceFrom_Open_OwnerAndDetail()
    {
        ProofContext context = Context([Item(new DoneRule.EvidenceFrom("review"))], evidence: []);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal(Quinn, item.Owner);
        Assert.Null(item.Detail);
    }

    /// <summary>A human note (an Evidence line with no command) never meets <c>evidence from</c>.</summary>
    [Fact]
    public void EvidenceFrom_NoteLine_DoesNotCount()
    {
        EvidenceLine note = new(default, Quinn, string.Empty, null, null, null, "Looked fine to me");
        ProofContext context = Context([Item(new DoneRule.EvidenceFrom(Verify))], evidence: [note]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal(Quinn, item.Owner);
    }

    /// <summary>A command by a member whose role does not cover the step does not meet it.</summary>
    [Fact]
    public void EvidenceFrom_AuthorWithoutCoverage_IsOpen()
    {
        ProofContext context = Context([Item(new DoneRule.EvidenceFrom(Verify))], evidence: [Run(Dev, "dotnet test", 0)]);

        Assert.False(Single(context).Met);
    }

    /// <summary>When no member covers the step, the open item has no owner.</summary>
    [Fact]
    public void EvidenceFrom_NobodyCoversStep_OwnerIsNull()
    {
        ProofContext context = Context([Item(new DoneRule.EvidenceFrom("deploy"))], evidence: []);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Null(item.Owner);
    }

    /// <summary>The latest Evidence line for the command having exit 0 meets <c>command exits 0</c>.</summary>
    [Fact]
    public void CommandExits_Met()
    {
        ProofContext context = Context([Item(new DoneRule.CommandExits("dotnet test"))], evidence: [Run(Dev, "dotnet test", 0)]);

        ProofItem item = Single(context);

        Assert.True(item.Met);
        Assert.Null(item.Owner);
        Assert.Null(item.Detail);
    }

    /// <summary>A run the Agent added a note to still meets the item, read back from the line as a Task file holds it.</summary>
    [Fact]
    public void CommandExits_RunWithANote_IsMet()
    {
        EvidenceLine stored = new(Noon, Dev, "dotnet test", 0, null, null, string.Empty, "14 tests on the merged branch");
        Assert.True(EvidenceLine.TryParse(stored.Format(), out EvidenceLine? parsed));
        Assert.NotNull(parsed);
        ProofContext context = Context([Item(new DoneRule.CommandExits("dotnet test"))], evidence: [parsed]);

        Assert.True(Single(context).Met);
    }

    /// <summary>A command never run leaves the item open, owned by the assignee, with the detail <c>not run</c>.</summary>
    [Fact]
    public void CommandExits_Open_OwnerAndDetail()
    {
        ProofContext context = Context([Item(new DoneRule.CommandExits("dotnet test"))], evidence: [Run(Dev, "dotnet build", 0)]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal(Dev, item.Owner);
        Assert.Equal("not run", item.Detail);
    }

    /// <summary>A failing latest run says <c>last run exit &lt;n&gt;</c> with its exit code.</summary>
    [Fact]
    public void CommandExits_LastRunFailed_SaysExitCode()
    {
        ProofContext context = Context([Item(new DoneRule.CommandExits("dotnet test"))], evidence: [Run(Dev, "dotnet test", 1)]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal(Dev, item.Owner);
        Assert.Equal("last run exit 1", item.Detail);
    }

    /// <summary>A failure after a pass is open: the latest run wins.</summary>
    [Fact]
    public void CommandExits_LatestRunWins()
    {
        ProofContext context = Context(
            [Item(new DoneRule.CommandExits("dotnet test"))],
            evidence: [Run(Dev, "dotnet test", 0, minutes: 0), Run(Dev, "dotnet test", 2, minutes: 5)]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal("last run exit 2", item.Detail);
    }

    /// <summary>A pass after a failure is met, whatever order the lines are stored in.</summary>
    [Fact]
    public void CommandExits_PassAfterFailure_IsMetRegardlessOfStoredOrder()
    {
        ProofContext context = Context(
            [Item(new DoneRule.CommandExits("dotnet test"))],
            evidence: [Run(Dev, "dotnet test", 0, minutes: 5), Run(Dev, "dotnet test", 1, minutes: 0)]);

        Assert.True(Single(context).Met);
    }

    /// <summary>The command is compared trimmed, so stray spaces in the Evidence line do not hide a run.</summary>
    [Fact]
    public void CommandExits_CommandComparedTrimmed()
    {
        ProofContext context = Context([Item(new DoneRule.CommandExits("dotnet test"))], evidence: [Run(Dev, "  dotnet test ", 0)]);

        Assert.True(Single(context).Met);
    }

    /// <summary>A child carrying the tag that is Done meets <c>task tagged</c>.</summary>
    [Fact]
    public void TaskTagged_Met()
    {
        TaskItem child = Child("RSH-0209", TaskState.Done, "Sam", "citation-check", "- [x] a\n- [x] b");
        ProofContext context = Context([Item(new DoneRule.TaskTagged("citation-check"))], children: [child]);

        ProofItem item = Single(context);

        Assert.True(item.Met);
        Assert.Null(item.Owner);
        Assert.Null(item.Detail);
    }

    /// <summary>An open tagged child names its assignee and counts checked boxes against all boxes, ignoring fenced code.</summary>
    [Fact]
    public void TaskTagged_CountsBoxesOutsideFences()
    {
        string description = string.Join(
            "\n",
            "Claims to check.",
            string.Join("\n", Enumerable.Repeat("- [x] cited", 19)),
            string.Join("\n", Enumerable.Repeat("- [ ] not yet", 4)),
            "```",
            "- [x] inside a fence",
            "- [ ] inside a fence",
            "```");
        TaskItem child = Child("RSH-0209", TaskState.InProgress, "Sam", "citation-check", description);
        ProofContext context = Context([Item(new DoneRule.TaskTagged("citation-check"))], children: [child]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal("Sam", item.Owner);
        Assert.Equal($"19 of 23 in #{child.Id}", item.Detail);
    }

    /// <summary>With no child carrying the tag the item says <c>no Task yet</c> and has no owner.</summary>
    [Fact]
    public void TaskTagged_NoChild_SaysNoTaskYet()
    {
        TaskItem other = Child("RSH-0210", TaskState.Done, "Sam", "other-tag", "- [x] a");
        ProofContext context = Context([Item(new DoneRule.TaskTagged("citation-check"))], children: [other]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Null(item.Owner);
        Assert.Equal("no Task yet", item.Detail);
    }

    /// <summary>A review after the last Created or Edited entry, by a member covering the step, meets <c>document reviewed by</c>.</summary>
    [Fact]
    public void DocumentReviewed_Met()
    {
        ProofContext context = Context(
            [Item(new DoneRule.DocumentReviewedBy("review"))],
            documents: [Doc("plan.md", Entry(0, Dev, DocChangeKind.Created), Entry(1, Ruth, DocChangeKind.Reviewed))]);

        ProofItem item = Single(context);

        Assert.True(item.Met);
        Assert.Null(item.Owner);
        Assert.Null(item.Detail);
    }

    /// <summary>No attached Markdown document: owner is the first member covering the step and the detail says so.</summary>
    [Fact]
    public void DocumentReviewed_NoAttachment_SaysNoDocumentAttached()
    {
        ProofContext context = Context([Item(new DoneRule.DocumentReviewedBy("review"))], documents: []);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal(Quinn, item.Owner);
        Assert.Equal("no document attached", item.Detail);
    }

    /// <summary>A document with only Created or Edited entries says <c>&lt;file&gt; — not reviewed yet</c>.</summary>
    [Fact]
    public void DocumentReviewed_NeverReviewed_IsOpen()
    {
        ProofContext context = Context(
            [Item(new DoneRule.DocumentReviewedBy("review"))],
            documents: [Doc("plan.md", Entry(0, Dev, DocChangeKind.Created))]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal(Quinn, item.Owner);
        Assert.Equal("plan.md — not reviewed yet", item.Detail);
    }

    /// <summary>A review followed by an edit no longer counts: <c>&lt;file&gt; — edited after &lt;name&gt;'s review</c>.</summary>
    [Fact]
    public void DocumentReviewed_EditedAfterReview_IsOpen()
    {
        ProofContext context = Context(
            [Item(new DoneRule.DocumentReviewedBy("review"))],
            documents: [Doc("plan.md", Entry(0, Dev, DocChangeKind.Created), Entry(1, Ruth, DocChangeKind.Reviewed), Entry(2, Dev, DocChangeKind.Edited))]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal(Quinn, item.Owner);
        Assert.Equal("plan.md — edited after Ruth's review", item.Detail);
    }

    /// <summary>A review followed by an objection is open: <c>&lt;file&gt; — &lt;name&gt; objected: &lt;summary&gt;</c>.</summary>
    [Fact]
    public void DocumentReviewed_ObjectedAfterReview_IsOpen()
    {
        ProofContext context = Context(
            [Item(new DoneRule.DocumentReviewedBy("review"))],
            documents: [Doc("plan.md", Entry(0, Dev, DocChangeKind.Created), Entry(1, Ruth, DocChangeKind.Reviewed), Entry(2, Quinn, DocChangeKind.Objected, "section 3 has no source"))]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal("plan.md — Quinn objected: section 3 has no source", item.Detail);
    }

    /// <summary>A new review after an objection meets the item again.</summary>
    [Fact]
    public void DocumentReviewed_ReviewedAfterObjection_IsMet()
    {
        ProofContext context = Context(
            [Item(new DoneRule.DocumentReviewedBy("review"))],
            documents: [Doc("plan.md", Entry(0, Dev, DocChangeKind.Created), Entry(1, Quinn, DocChangeKind.Objected, "gaps"), Entry(2, Ruth, DocChangeKind.Reviewed))]);

        Assert.True(Single(context).Met);
    }

    /// <summary>A review by someone whose role does not cover the step does not count.</summary>
    [Fact]
    public void DocumentReviewed_ReviewerWithoutCoverage_IsOpen()
    {
        ProofContext context = Context(
            [Item(new DoneRule.DocumentReviewedBy("review"))],
            documents: [Doc("plan.md", Entry(0, Ruth, DocChangeKind.Created), Entry(1, Dev, DocChangeKind.Reviewed))]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal("plan.md — not reviewed yet", item.Detail);
    }

    /// <summary>With several attached documents the item is met when any one meets it.</summary>
    [Fact]
    public void DocumentReviewed_TwoDocuments_OneMeetsIt_IsMet()
    {
        ProofContext context = Context(
            [Item(new DoneRule.DocumentReviewedBy("review"))],
            documents:
            [
                Doc("draft.md", Entry(0, Dev, DocChangeKind.Created)),
                Doc("final.md", Entry(0, Dev, DocChangeKind.Created), Entry(1, Ruth, DocChangeKind.Reviewed)),
            ]);

        Assert.True(Single(context).Met);
    }

    /// <summary>With several documents and none met, the detail names the one closest to meeting it (reviewed but edited beats never reviewed).</summary>
    [Fact]
    public void DocumentReviewed_TwoDocumentsNoneMet_NamesTheClosest()
    {
        ProofContext context = Context(
            [Item(new DoneRule.DocumentReviewedBy("review"))],
            documents:
            [
                Doc("draft.md", Entry(0, Dev, DocChangeKind.Created)),
                Doc("final.md", Entry(0, Dev, DocChangeKind.Created), Entry(1, Ruth, DocChangeKind.Reviewed), Entry(2, Dev, DocChangeKind.Edited)),
            ]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal("final.md — edited after Ruth's review", item.Detail);
    }

    /// <summary>"Later than" follows the entries' order in the file, not their timestamps.</summary>
    [Fact]
    public void DocumentReviewed_OrderIsFileOrder_NotTimestamp()
    {
        DocHistoryEntry created = new(Noon.AddHours(5), Dev, DocActorKind.Agent, DocChangeKind.Created, "made");
        DocHistoryEntry reviewed = new(Noon, Ruth, DocActorKind.Agent, DocChangeKind.Reviewed, "ok");
        ProofContext context = Context(
            [Item(new DoneRule.DocumentReviewedBy("review"))],
            documents: [new ProofDocument("plan.md", [created, reviewed])]);

        Assert.True(Single(context).Met);
    }

    /// <summary>An unreadable rule is never met and says <c>Huddle can't read this rule</c>.</summary>
    [Fact]
    public void Unreadable_IsNeverMet()
    {
        ProofContext context = Context([Item(new DoneRule.Unreadable("something odd"))]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Null(item.Owner);
        Assert.Equal("Huddle can't read this rule", item.Detail);
    }

    /// <summary>The enabled QA gate adds "QA evidence" from the Gate source, met by an Evidence line from a member who verifies.</summary>
    [Fact]
    public void QaGateEnabled_AddsQaEvidenceOnce()
    {
        ProofContext context = Context([], gates: [GateId.QaEvidenceBeforeDone], evidence: [Run(Quinn, "dotnet test", 0)]);

        ProofItem item = Single(context);

        Assert.Equal("QA evidence", item.Label);
        Assert.Equal(ProofItemSource.Gate, item.Source);
        Assert.True(item.Met);
    }

    /// <summary>Open "QA evidence" is owned by the first member covering verify.</summary>
    [Fact]
    public void QaGateEnabled_NoEvidence_IsOpenWithOwner()
    {
        ProofContext context = Context([], gates: [GateId.QaEvidenceBeforeDone]);

        ProofItem item = Single(context);

        Assert.False(item.Met);
        Assert.Equal(Quinn, item.Owner);
    }

    /// <summary>An item that already says <c>evidence from verify</c> suppresses the gate's own "QA evidence".</summary>
    [Fact]
    public void QaGateEnabled_ItemAlreadyEvidenceFromVerify_IsNotAddedTwice()
    {
        ProofContext context = Context(
            [new DoneItem("Verified by QA", new DoneRule.EvidenceFrom("Verify"), ProofItemSource.Process)],
            gates: [GateId.QaEvidenceBeforeDone]);

        ProofItem item = Single(context);

        Assert.Equal("Verified by QA", item.Label);
        Assert.Equal(ProofItemSource.Process, item.Source);
    }

    /// <summary>With the QA gate not enabled nothing is added.</summary>
    [Fact]
    public void QaGateDisabled_AddsNothing()
    {
        ProofContext context = Context([], gates: [GateId.ChecksBeforeMerge, GateId.SpecApprovedBeforeBuild]);

        Assert.Empty(ProofEvaluator.Evaluate(TestTasks.Make(assignee: Dev), context).Items);
    }

    /// <summary>The failing-test gate adds "A test failed before the fix" for a bug.</summary>
    [Fact]
    public void FailingTestGate_AddsItemForBugsOnly()
    {
        ProofContext bug = Context([], gates: [GateId.FailingTestBeforeFix], isBug: true);
        ProofContext notBug = Context([], gates: [GateId.FailingTestBeforeFix], isBug: false);

        ProofItem item = Single(bug);

        Assert.Equal("A test failed before the fix", item.Label);
        Assert.Equal(ProofItemSource.Gate, item.Source);
        Assert.Empty(ProofEvaluator.Evaluate(TestTasks.Make(assignee: Dev), notBug).Items);
    }

    /// <summary>The failing-test item is met by a non-zero exit followed by a later exit 0, and open otherwise.</summary>
    [Fact]
    public void FailingTestGate_MetByFailureThenPass()
    {
        ProofContext met = Context([], gates: [GateId.FailingTestBeforeFix], isBug: true, evidence: [Run(Dev, "dotnet test", 1, minutes: 0), Run(Dev, "dotnet test", 0, minutes: 3)]);
        ProofContext passOnly = Context([], gates: [GateId.FailingTestBeforeFix], isBug: true, evidence: [Run(Dev, "dotnet test", 0)]);

        Assert.True(Single(met).Met);
        Assert.False(Single(passOnly).Met);
    }

    /// <summary>Process items come first in their written order, then "QA evidence", then the failing-test item.</summary>
    [Fact]
    public void Evaluate_OrdersProcessItemsThenGates()
    {
        ProofContext context = Context(
            [Item(new DoneRule.Unreadable("x"), "First"), Item(new DoneRule.CommandExits("dotnet test"), "Second")],
            gates: [GateId.FailingTestBeforeFix, GateId.QaEvidenceBeforeDone],
            isBug: true);

        Proof proof = ProofEvaluator.Evaluate(TestTasks.Make(assignee: Dev), context);

        Assert.Equal(["First", "Second", "QA evidence", "A test failed before the fix"], proof.Items.Select(static item => item.Label));
    }

    /// <summary>An empty Definition with no gates is complete with zero items.</summary>
    [Fact]
    public void EmptyDefinition_IsCompleteWithZeroItems()
    {
        Proof proof = ProofEvaluator.Evaluate(TestTasks.Make(assignee: Dev), Context([]));

        Assert.Empty(proof.Items);
        Assert.Equal(0, proof.Total);
        Assert.Equal(0, proof.MetCount);
        Assert.True(proof.IsComplete);
        Assert.True(Proof.Empty.IsComplete);
    }

    /// <summary>MetCount counts met items, Total counts all, and IsComplete needs every one.</summary>
    [Fact]
    public void Proof_MetCountTotalAndIsComplete()
    {
        ProofContext context = Context(
            [Item(new DoneRule.CommandExits("dotnet test")), Item(new DoneRule.Unreadable("x"))],
            evidence: [Run(Dev, "dotnet test", 0)]);

        Proof proof = ProofEvaluator.Evaluate(TestTasks.Make(assignee: Dev), context);

        Assert.Equal(1, proof.MetCount);
        Assert.Equal(2, proof.Total);
        Assert.False(proof.IsComplete);
    }

    /// <summary>Process items keep the label and source the Definition gave them.</summary>
    [Fact]
    public void Evaluate_KeepsLabelAndProcessSource()
    {
        ProofContext context = Context([Item(new DoneRule.Unreadable("x"), Reviewed)]);

        ProofItem item = Single(context);

        Assert.Equal(Reviewed, item.Label);
        Assert.Equal(ProofItemSource.Process, item.Source);
    }

    /// <summary>Evaluates the Task assigned to Dev against <paramref name="context"/> and returns its only item.</summary>
    /// <param name="context">The context.</param>
    private static ProofItem Single(ProofContext context) =>
        Assert.Single(ProofEvaluator.Evaluate(TestTasks.Make(assignee: Dev), context).Items);

    /// <summary>A Process-sourced Definition item.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="label">The label.</param>
    private static DoneItem Item(DoneRule rule, string label = "Item") => new(label, rule, ProofItemSource.Process);

    /// <summary>A context over the shared roster and coverage map.</summary>
    /// <param name="definition">The Definition of Done items.</param>
    /// <param name="gates">The enabled ADR-0039 gates.</param>
    /// <param name="isBug">Whether the Task is a bug.</param>
    /// <param name="evidence">The Task's Evidence lines.</param>
    /// <param name="children">The Task's children.</param>
    /// <param name="documents">The attached documents' histories.</param>
    private static ProofContext Context(
        IReadOnlyList<DoneItem> definition,
        IReadOnlyList<GateId>? gates = null,
        bool isBug = false,
        IReadOnlyList<EvidenceLine>? evidence = null,
        IReadOnlyList<TaskItem>? children = null,
        IReadOnlyList<ProofDocument>? documents = null) =>
        new(definition, gates ?? [], isBug, evidence ?? [], Covers, Roster, children ?? [], documents ?? []);

    /// <summary>A structured Evidence line.</summary>
    /// <param name="author">Who ran it.</param>
    /// <param name="command">The command.</param>
    /// <param name="exitCode">Its exit code.</param>
    /// <param name="minutes">Minutes after noon.</param>
    private static EvidenceLine Run(string author, string command, int exitCode, int minutes = 0) =>
        new(Noon.AddMinutes(minutes), author, command, exitCode, null, null, $"{author} | {command}");

    /// <summary>A child Task of the Task under test.</summary>
    /// <param name="id">The child's id.</param>
    /// <param name="status">The child's status.</param>
    /// <param name="assignee">The child's assignee.</param>
    /// <param name="tag">The child's tag.</param>
    /// <param name="description">The child's description.</param>
    private static TaskItem Child(string id, TaskState status, string assignee, string tag, string description) =>
        TestTasks.Make(id: id, status: status, assignee: assignee, tags: [tag], description: description, parent: new TaskId("PLAT", 1));

    /// <summary>A document history, entries in file order.</summary>
    /// <param name="file">The file name.</param>
    /// <param name="entries">The entries, oldest first.</param>
    private static ProofDocument Doc(string file, params DocHistoryEntry[] entries) => new(file, entries);

    /// <summary>One history entry.</summary>
    /// <param name="minutes">Minutes after noon.</param>
    /// <param name="actor">Who made it.</param>
    /// <param name="kind">What kind of change.</param>
    /// <param name="summary">The summary.</param>
    private static DocHistoryEntry Entry(int minutes, string actor, DocChangeKind kind, string summary = "s") =>
        new(Noon.AddMinutes(minutes), actor, DocActorKind.Agent, kind, summary);
}
