using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Tasks.HumanGate;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Issue #416: in a Team with <c>accept-before-merge</c>, a finished Task must reach the Human even when no Agent set an Action.
/// In ZERO-26 the Team asked for acceptance in chat, the Task stayed in Review with no Action, and nothing could be accepted. When
/// the Proof of such a Task completes, Huddle names the merge as its Action and moves it to Needs You, in one write that wakes nobody.
/// </summary>
public sealed class AcceptGateNamesMergeTests
{
    private const string Team = "Platform";
    private const string AcceptGate = "'accept-before-merge'";
    private const string FeatureMerge = "git merge --no-ff feat/rush-flag";

    private static readonly DateTimeOffset Noon = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>Evidence that completes the Proof of an ungated Task in Review names the merge and moves it to Needs You, as Huddle, in one change.</summary>
    [Fact]
    public void AddEvidence_UngatedTaskProofCompletes_NamesTheMergeAndMovesToNeedsYou()
    {
        using Lane lane = new(AcceptGate);
        TaskItem task = lane.CreateInReview();
        List<TaskChange> changes = [];
        lane.Harness.Events.TaskChanged += changes.Add;
        lane.Proof.Current = FixedProofSource.Complete;

        lane.AddEvidence(task);

        TaskItem? after = lane.Harness.Store.Get(task.Id);
        Assert.NotNull(after);
        Assert.Equal(TaskState.NeedsYou, after.Status);
        Assert.Equal(HumanGateRules.MergeAction, after.Action);
        Assert.Equal("Huddle", after.ChangeLog[^1].Actor);
        Assert.Equal(2, changes.Count);
        Assert.True(TaskActors.IsReadyMove(changes[1]));
    }

    /// <summary>An Agent moving an ungated Task to Review with its Proof already complete finds the merge named and the Task in Needs You.</summary>
    [Fact]
    public void Update_AgentMovesToReviewWithProofComplete_NamesTheMergeAndMovesToNeedsYou()
    {
        using Lane lane = new(AcceptGate, FixedProofSource.Complete);
        TaskItem task = lane.Create(TaskState.InProgress);

        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Review }, null, Nova));

        TaskItem? after = lane.Harness.Store.Get(task.Id);
        Assert.Equal(TaskState.NeedsYou, after?.Status);
        Assert.Equal(HumanGateRules.MergeAction, after?.Action);
    }

    /// <summary>While the Proof is open nothing changes: the Task stays in Review with no Action.</summary>
    [Fact]
    public void AddEvidence_ProofOpen_StaysInReviewWithNoAction()
    {
        using Lane lane = new(AcceptGate);
        TaskItem task = lane.CreateInReview();

        lane.AddEvidence(task);

        TaskItem? after = lane.Harness.Store.Get(task.Id);
        Assert.Equal(TaskState.Review, after?.Status);
        Assert.Null(after?.Action);
    }

    /// <summary>A Team without the gate keeps its route: an ungated Task goes from Review to Done, and gets no Action.</summary>
    [Fact]
    public void AddEvidence_TeamWithoutTheGate_StaysInReviewWithNoAction()
    {
        using Lane lane = new("'checks-before-merge'");
        TaskItem task = lane.CreateInReview();
        lane.Proof.Current = FixedProofSource.Complete;

        lane.AddEvidence(task);

        TaskItem? after = lane.Harness.Store.Get(task.Id);
        Assert.Equal(TaskState.Review, after?.Status);
        Assert.Null(after?.Action);
    }

    /// <summary>An Action an Agent already wrote is the one the Human accepts: Huddle does not replace it.</summary>
    [Fact]
    public void AddEvidence_TaskAlreadyHasAnAction_KeepsIt()
    {
        using Lane lane = new(AcceptGate);
        TaskItem task = lane.CreateInReview();
        TaskPatch patch = new() { Action = Optional<string?>.Set("Merge feat/rush-flag into main") };
        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, patch, null, Nova));
        lane.Proof.Current = FixedProofSource.Complete;

        lane.AddEvidence(task);

        TaskItem? after = lane.Harness.Store.Get(task.Id);
        Assert.Equal(TaskState.NeedsYou, after?.Status);
        Assert.Equal("Merge feat/rush-flag into main", after?.Action);
    }

    /// <summary>An Action the Human cleared stays cleared: the Human's word is final, and Huddle does not put it back.</summary>
    [Fact]
    public void AddEvidence_HumanClearedTheAction_StaysUngated()
    {
        using Lane lane = new(AcceptGate);
        TaskItem task = lane.CreateInReview();
        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, new TaskPatch { Action = Optional<string?>.Set("Merge it") }, null, Nova));
        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, new TaskPatch { Action = Optional<string?>.Set(null) }, null, lane.Human));
        lane.Proof.Current = FixedProofSource.Complete;

        lane.AddEvidence(task);

        TaskItem? after = lane.Harness.Store.Get(task.Id);
        Assert.Equal(TaskState.Review, after?.Status);
        Assert.Null(after?.Action);
    }

    /// <summary>A Human gate the Human turned off stays off in the same way.</summary>
    [Fact]
    public void AddEvidence_HumanTurnedTheGateOff_StaysUngated()
    {
        using Lane lane = new(AcceptGate);
        TaskItem task = lane.CreateInReview();
        TaskPatch on = new() { HumanGate = Optional<bool>.Set(true), Reason = "Goes to main" };
        TaskPatch off = new() { HumanGate = Optional<bool>.Set(false) };
        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, on, null, Nova));
        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, off, null, lane.Human));
        lane.Proof.Current = FixedProofSource.Complete;

        lane.AddEvidence(task);

        TaskItem? after = lane.Harness.Store.Get(task.Id);
        Assert.Equal(TaskState.Review, after?.Status);
        Assert.Null(after?.Action);
    }

    /// <summary>The Human's own change moves nothing, as for any gated Task: Huddle acts only on an Agent's write.</summary>
    [Fact]
    public void Update_HumanMovesToReviewWithProofComplete_StaysInReview()
    {
        using Lane lane = new(AcceptGate, FixedProofSource.Complete);
        TaskItem task = lane.Create(TaskState.InProgress);

        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Review }, null, lane.Human));

        TaskItem? after = lane.Harness.Store.Get(task.Id);
        Assert.Equal(TaskState.Review, after?.Status);
        Assert.Null(after?.Action);
    }

    /// <summary>Evidence on a Task still In Progress does not skip Review.</summary>
    [Fact]
    public void AddEvidence_InProgress_StaysInProgressWithNoAction()
    {
        using Lane lane = new(AcceptGate, FixedProofSource.Complete);
        TaskItem task = lane.Create(TaskState.InProgress);

        lane.AddEvidence(task);

        TaskItem? after = lane.Harness.Store.Get(task.Id);
        Assert.Equal(TaskState.InProgress, after?.Status);
        Assert.Null(after?.Action);
    }

    /// <summary>A Task with nothing to merge has a way out with no Accept: the Human clears the named Action, then moves it to Done.</summary>
    [Fact]
    public void HumanClearsTheNamedActionThenMovesToDone_IsAllowed()
    {
        using Lane lane = new(AcceptGate);
        TaskItem task = lane.CreateInReview();
        lane.Proof.Current = FixedProofSource.Complete;
        lane.AddEvidence(task);

        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, new TaskPatch { Action = Optional<string?>.Set(null) }, null, lane.Human));
        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, lane.Human));

        Assert.Equal(TaskState.Done, lane.Harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>
    /// The whole route ZERO-26 could not take: the merge is refused, the Evidence that completes the Proof brings the Task to the Human,
    /// one Accept lets the merge run and keeps the Task open for it, and the Task then moves to Done with no second Accept.
    /// </summary>
    [Fact]
    public void WholeRoute_RefusedThenOneAcceptThenMergeThenDone()
    {
        using Lane lane = new(AcceptGate);
        TaskItem task = lane.CreateInReview();
        Assert.NotNull(lane.MergeGate.RefusalFor("Nova", FeatureMerge));
        lane.Proof.Current = FixedProofSource.Complete;

        lane.AddEvidence(task);
        Assert.NotNull(lane.MergeGate.RefusalFor("Nova", FeatureMerge));

        TaskItem waiting = Assert.IsType<TaskItem>(lane.Harness.Store.Get(task.Id));
        TaskItem accepted = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Decide(task.Id, TaskDecision.Accept, null, waiting.Version, lane.Human)).Task;

        Assert.Equal(TaskState.NeedsYou, accepted.Status);
        Assert.Null(lane.MergeGate.RefusalFor("Nova", FeatureMerge));
        _ = Assert.IsType<TaskResult.Saved>(lane.Harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, Nova));
        Assert.Equal(TaskState.Done, lane.Harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A Team with the given gates, on a harness whose <see cref="TaskService"/> and merge gate read the same process.</summary>
    private sealed class Lane : IDisposable
    {
        private readonly TempDataDir dir = new();

        /// <summary>Builds the lane.</summary>
        /// <param name="gates">The gates line.</param>
        /// <param name="proof">The Proof every Task starts with; three of four met when <see langword="null"/>.</param>
        public Lane(string gates, Proof? proof = null)
        {
            VersionedRef type = new("web", "1.2.0");
            LibraryRoot root = new("teams", "Teams", this.dir.Path, LibraryRootKind.Teams);
            Assert.True(TeamFile.TryParse($"---\nteamType: 'web@1.2.0'\ngates: [{gates}]\ncheckCommand: 'dotnet test'\n---\n", out TeamFile? teamFile));
            FakeTeamFolders folders = new()
            {
                TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, this.dir.Path, LibraryNodeRole.TeamFolder)),
                ReadResult = teamFile,
            };
            FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], [], true) { TeamType = type }] };
            ProcessGateService service = new(catalog, folders, RoleFixtures.Types(this.dir));
            ManualTimeProvider clock = new(Noon);
            this.Proof = new FixedProofSource(proof ?? FixedProofSource.ThreeOfFour);
            this.Harness = new TaskToolHarness(clock: clock, gates: service, proof: this.Proof);
            this.MergeGate = new ProcessMergeGate(this.Harness.Personas, this.Harness.Store, service, catalog, clock);
        }

        /// <summary>The harness.</summary>
        public TaskToolHarness Harness { get; }

        /// <summary>The Proof every Task reports.</summary>
        public FixedProofSource Proof { get; }

        /// <summary>The merge gate on the same Tasks and process.</summary>
        public ProcessMergeGate MergeGate { get; }

        /// <summary>The Human's actor.</summary>
        public TaskActor Human => TaskActors.Human(this.Harness.Options.Value);

        /// <summary>Creates an ungated Task assigned to Nova, as the Human, so creating it moves nothing.</summary>
        /// <param name="status">The status to create it in.</param>
        public TaskItem Create(TaskState status)
        {
            TaskDraft draft = new("Add the rush flag", Team, null, status, Assignee: "Nova", Description: "Body");
            return Assert.IsType<TaskResult.Saved>(this.Harness.Service.Create(draft, this.Human)).Task;
        }

        /// <summary>Creates an ungated Task in Review.</summary>
        public TaskItem CreateInReview() => this.Create(TaskState.Review);

        /// <summary>Adds an Evidence line as Nova.</summary>
        /// <param name="task">The Task.</param>
        public void AddEvidence(TaskItem task)
        {
            EvidenceLine line = new(Noon, "Nova", "dotnet test", 0, null, null, "raw");
            _ = Assert.IsType<TaskResult.Saved>(this.Harness.Service.AddEvidence(task.Id, line, Nova));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Harness.Dispose();
            this.dir.Dispose();
        }
    }
}
