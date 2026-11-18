using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.Tests.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// <see cref="ProcessMergeGate"/> maps a Persona's merge or push to its Team's <c>checks-before-merge</c>
/// gate, reading Evidence from the Team's Task files (PR-7, Spec §Gates).
/// </summary>
public sealed class ProcessMergeGateTests
{
    private const string Team = "Platform";
    private const string ChecksFirst = "Gate checks-before-merge: an Evidence line with exit code 0 for 'dotnet test' is needed before merging or pushing.";

    private const string NoTeamPush = "Cannot push: a push waits for the Human's Accept on a Task, and you belong to no Team, so no Task of yours can carry one. Ask the Human to push.";

    private const string OldTest = "dotnet test (repo root C:\\HuddleZero\\repos\\bakery-quote)";

    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A Persona whose Team has the gate and no passing check Evidence is refused with the gate message.</summary>
    [Fact]
    public void RefusalFor_GateOnNoEvidence_ReturnsGateMessage()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A passing check recorded on any Task of the Persona's Team lets the merge through.</summary>
    [Fact]
    public void RefusalFor_PassingEvidenceOnATeamTask_IsAllowed()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, "dotnet test", 0);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Null(gate.RefusalFor("Nova", "git merge main"));
    }

    /// <summary>A failing run is not a pass, so the merge stays refused.</summary>
    [Fact]
    public void RefusalFor_FailingEvidence_IsRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, "dotnet test", 1);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A Team whose process does not enable <c>checks-before-merge</c> is not held by it; a line that puts nothing on main and pushes nothing passes.</summary>
    [Fact]
    public void RefusalFor_ChecksGateNotEnabled_IsAllowed()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.Null(gate.RefusalFor("Nova", "git merge main"));
    }

    /// <summary>A Persona in no Team has every push refused and no merge held; a Persona that cannot be resolved fails closed (review fix item 5).</summary>
    [Fact]
    public void RefusalFor_PersonaInNoTeamPushRefusedUnknownRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(NoTeamPush, gate.RefusalFor("Kai", "git push origin feat/x"));
        Assert.Null(gate.RefusalFor("Kai", "git merge main"));
        Assert.Equal("Cannot check the merge gate: the Persona 'Ghost' could not be resolved.", gate.RefusalFor("Ghost", "git push"));
    }

    /// <summary>Passing Evidence on a Task that is already Done is stale and does not satisfy the merge gate (review fix item 6).</summary>
    [Fact]
    public void RefusalFor_EvidenceOnlyOnDoneTask_IsRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, "dotnet test", 0);
        TaskItem task = Assert.Single(harness.Store.All);
        TaskResult moved = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, TaskActors.Human(harness.Options.Value));
        Assert.IsType<TaskResult.Saved>(moved);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A passing run recorded before #340, with its note folded into the command, lets the merge through.</summary>
    [Fact]
    public void RefusalFor_OldLineWithNoteInCommand_ExitZero_IsAllowed()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, OldTest, 0);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Null(gate.RefusalFor("Nova", "git merge main"));
    }

    /// <summary>A failing run recorded before #340 keeps the merge refused.</summary>
    [Fact]
    public void RefusalFor_OldLineWithNoteInCommand_ExitOne_IsRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, OldTest, 1);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>An old passing line followed by a new failing run is refused: the latest run wins.</summary>
    [Fact]
    public void RefusalFor_OldPassThenNewFailure_IsRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, OldTest, 0);
        AddEvidence(harness, "dotnet test", 1, minutes: 5);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>In a Team whose gates line lacks <c>accept-before-merge</c> the assignee's merge is refused with no Accept, and the refusal names the Task.</summary>
    [Fact]
    public void RefusalFor_GatedUnacceptedTaskOfAssignee_RefusesMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        TaskItem task = AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.InProgress);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        string? refusal = gate.RefusalFor("Nova", "git merge --no-ff feat/quote-calculator");

        Assert.Equal(
            $"Cannot merge: a merge into main waits for the Human's Accept, and no Task of your Team has one. Put the merge in Task {task.Id}'s Action with update_task, for example 'Merge feat/x into main'. Huddle moves the Task to Needs You when its Proof is complete; merge after the Human accepts it. Do not ask for the Accept in chat.",
            refusal);
    }

    /// <summary>Every push is refused with no Accept that names a push, a feature branch included; the gates line makes no difference.</summary>
    [Fact]
    public void RefusalFor_GatedUnacceptedTask_EveryPushIsRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.InProgress);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.NotNull(gate.RefusalFor("Nova", "git push origin main"));
        Assert.NotNull(gate.RefusalFor("Nova", "git push"));
        Assert.NotNull(gate.RefusalFor("Nova", "git push origin HEAD:refs/heads/master"));
        Assert.NotNull(gate.RefusalFor("Nova", "git push -u origin feat/quote-calculator"));
    }

    /// <summary>A valid Accept lets the assignee merge.</summary>
    [Fact]
    public void RefusalFor_GatedTaskWithValidAccept_AllowsMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: true, TaskState.NeedsYou);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.Null(gate.RefusalFor("Nova", "git merge --no-ff feat/quote-calculator"));
    }

    /// <summary>With no Accept on any Task of the Team, the merge is refused whoever's Tasks are unaccepted or ungated: the gate no longer depends on an open gated Task.</summary>
    [Fact]
    public void RefusalFor_NotAssigneeOrNotGated_StillRefusesMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Kai", gated: true, accepted: false, TaskState.InProgress);
        AddGatedTask(harness, "Nova", gated: false, accepted: false, TaskState.InProgress);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.NotNull(gate.RefusalFor("Nova", "git merge feat/quote-calculator"));
    }

    /// <summary>A Cancelled gated Task carries no Accept, so the merge stays refused.</summary>
    [Fact]
    public void RefusalFor_ClosedGatedTask_StillRefusesMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.Cancelled);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.NotNull(gate.RefusalFor("Nova", "git merge feat/quote-calculator"));
    }

    /// <summary>Merging main into a feature branch is routine work and stays allowed, however main is spelled.</summary>
    [Theory]
    [InlineData("git merge main")]
    [InlineData("git merge origin/main")]
    [InlineData("git merge --no-edit origin/main")]
    [InlineData("git -C repo merge main")]
    public void RefusalFor_GatedUnacceptedTask_MergeOfMainIsAllowed(string command)
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.InProgress);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.Null(gate.RefusalFor("Nova", command));
    }

    /// <summary>With both rules on, the checks gate still speaks first, and passing checks do not lift the Accept rule.</summary>
    [Fact]
    public void RefusalFor_BothRulesOn_ChecksFirstThenAccept()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.InProgress);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git merge feat/quote-calculator"));

        AddEvidence(harness, "dotnet test", 0);

        string? refusal = gate.RefusalFor("Nova", "git merge feat/quote-calculator");
        Assert.StartsWith("Cannot merge: a merge into main waits for the Human's Accept", refusal, StringComparison.Ordinal); // contains-ok: the full wording is pinned in RefusalFor_GatedUnacceptedTaskOfAssignee_RefusesMerge.
    }

    private const string AcceptGate = "'accept-before-merge'";
    private const string FeatureMerge = "git merge --no-ff feat/quote-calculator";

    /// <summary>With the gate on and no accepted Task, a merge into main is refused even when no Task names a merge, and the refusal teaches the way forward.</summary>
    [Fact]
    public void RefusalFor_AcceptGateNoAcceptedTask_RefusesAndTeaches()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Quote calculator", Team, null, TaskState.InProgress, Assignee: "Nova"), TaskActors.Human(harness.Options.Value))).Task;
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);

        Assert.Equal(
            $"Cannot merge: a merge into main waits for the Human's Accept, and no Task of your Team has one. Put the merge in Task {task.Id}'s Action with update_task, for example 'Merge feat/x into main'. Huddle moves the Task to Needs You when its Proof is complete; merge after the Human accepts it. Do not ask for the Accept in chat.",
            gate.RefusalFor("Nova", FeatureMerge));
        Assert.NotNull(gate.RefusalFor("Nova", "git push origin main"));
    }

    /// <summary>With no obvious Task the refusal names no Task but still says where the merge goes.</summary>
    [Fact]
    public void RefusalFor_AcceptGateNoTasks_RefusalNamesNoTask()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);

        Assert.StartsWith("Cannot merge: a merge into main waits for the Human's Accept, and no Task of your Team has one. Put the merge in the Action of the Task this merge belongs to with update_task", gate.RefusalFor("Nova", FeatureMerge), StringComparison.Ordinal); // contains-ok: the tail is pinned in the test above.
    }

    /// <summary>Any member may merge once a Task of the Team has a valid Accept, not only the Task's assignee.</summary>
    [Fact]
    public void RefusalFor_AcceptGateTaskAcceptedForAnotherAssignee_AllowsMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Kai", gated: true, accepted: true, TaskState.NeedsYou);
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);

        Assert.Null(gate.RefusalFor("Nova", FeatureMerge));
    }

    /// <summary>A feature branch taking main still runs under the gate; a push of the feature branch does not (issue #602).</summary>
    [Fact]
    public void RefusalFor_AcceptGateMergeOfMain_IsAllowedButAFeaturePushIsNot()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);

        Assert.Null(gate.RefusalFor("Nova", "git merge origin/main"));
        Assert.NotNull(gate.RefusalFor("Nova", "git push -u origin feat/x"));
    }

    /// <summary>An Accept that took a Task with no Action straight to Done lets the merge run up to the last instant of the one-hour window.</summary>
    [Fact]
    public void RefusalFor_AcceptGateAcceptedStraightToDone_AllowsMergeAtTheWindowEdge()
    {
        ManualTimeProvider clock = new(Noon);
        using TaskToolHarness harness = new(clock: clock, proof: new FixedProofSource(FixedProofSource.Complete));
        using TempDataDir dir = new();
        AcceptStraightToDone(harness);
        ProcessMergeGate gate = Build(harness, dir, AcceptGate, clock);

        Assert.Null(gate.RefusalFor("Nova", FeatureMerge));

        clock.Advance(TimeSpan.FromHours(1));

        Assert.Null(gate.RefusalFor("Nova", FeatureMerge));
    }

    /// <summary>One tick past the one-hour window the Accept no longer carries a merge.</summary>
    [Fact]
    public void RefusalFor_AcceptGateAcceptedStraightToDone_RefusesJustAfterTheWindow()
    {
        ManualTimeProvider clock = new(Noon);
        using TaskToolHarness harness = new(clock: clock, proof: new FixedProofSource(FixedProofSource.Complete));
        using TempDataDir dir = new();
        AcceptStraightToDone(harness);
        ProcessMergeGate gate = Build(harness, dir, AcceptGate, clock);

        clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromTicks(1));

        Assert.NotNull(gate.RefusalFor("Nova", FeatureMerge));
    }

    private static void AcceptStraightToDone(TaskToolHarness harness)
    {
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Quote calculator", Team, null, Assignee: "Nova") { HumanGate = true }, human)).Task;
        task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, new TaskActor(TaskActorKind.Agent, "Nova", "id"))).Task;
        task = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, human)).Task;
        Assert.Equal(TaskState.Done, task.Status);
    }

    /// <summary>The last live failure: an ungated Task in Review, merge refused, Action set, Task reaches Needs You by itself, Accept, merge allowed, Done.</summary>
    [Fact]
    public void RefusalFor_UngatedReviewTask_ActionLeadsToAcceptThenMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskActor nova = new(TaskActorKind.Agent, "Nova", "id");
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Quote calculator", Team, null, TaskState.InProgress, Assignee: "Nova"), human)).Task;
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);
        Assert.NotNull(gate.RefusalFor("Nova", FeatureMerge));

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Action = Optional<string?>.Set("Merge feat/quote-calculator into main") }, null, nova));
        task = Assert.IsType<TaskItem>(harness.Store.Get(task.Id));

        Assert.Equal(TaskState.NeedsYou, task.Status);
        Assert.NotNull(gate.RefusalFor("Nova", FeatureMerge));

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, human));

        Assert.Null(gate.RefusalFor("Nova", FeatureMerge));

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, nova));
        Assert.Equal(TaskState.Done, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Spellings that put work on main are refused in a Team with the gate and no Accept.</summary>
    /// <param name="command">The shell line.</param>
    [Theory]
    [InlineData("git push origin HEAD")]
    [InlineData("git push -u origin HEAD")]
    [InlineData("git push origin +main")]
    [InlineData("git push origin 2>&1")]
    [InlineData("git push origin HEAD:main")]
    [InlineData("git push origin feat:main")]
    [InlineData("git push --all origin")]
    [InlineData("GIT_EDITOR=true git merge feat/x")]
    [InlineData("bash -c \"git merge feat/x\"")]
    [InlineData("pwsh -Command \"git merge feat/x\"")]
    [InlineData("git merge -")]
    [InlineData("git pull origin feat/x")]
    [InlineData("git -C \"a(b)\" merge feat/x")]
    public void RefusalFor_AcceptGateNoAccept_RefusesTheSpelling(string command)
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);

        Assert.NotNull(gate.RefusalFor("Nova", command));
    }

    /// <summary>Taking main into a feature branch and pulling main are not held, whatever redirection or option surrounds them.</summary>
    /// <param name="command">The shell line.</param>
    [Theory]
    [InlineData("git merge origin/main 2>&1")]
    [InlineData("git merge -m \"sync\" main")]
    [InlineData("git merge -X theirs main")]
    [InlineData("git merge --no-ff -m sync origin/main")]
    [InlineData("git pull")]
    [InlineData("git pull origin main")]
    [InlineData("git pull --rebase origin main")]
    public void RefusalFor_AcceptGateNoAccept_AllowsTheSpelling(string command)
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);

        Assert.Null(gate.RefusalFor("Nova", command));
    }

    /// <summary>A valid Accept on an open Task carries a merge to the last instant of its 24 hours.</summary>
    [Fact]
    public void RefusalFor_AcceptOnOpenTask_AllowsMergeAtTwentyFourHours()
    {
        ManualTimeProvider clock = new(Noon);
        using TaskToolHarness harness = new(clock: clock, proof: new FixedProofSource(FixedProofSource.Complete));
        using TempDataDir dir = new();
        _ = AddGatedTask(harness, "Kai", gated: true, accepted: true, TaskState.NeedsYou);
        ProcessMergeGate gate = Build(harness, dir, AcceptGate, clock);

        clock.Advance(TimeSpan.FromHours(24));

        Assert.Null(gate.RefusalFor("Nova", FeatureMerge));
    }

    /// <summary>One tick past 24 hours the Accept is old: the merge is refused and the refusal says so and how to get a fresh one.</summary>
    [Fact]
    public void RefusalFor_AcceptOnOpenTask_RefusesJustAfterTwentyFourHours()
    {
        ManualTimeProvider clock = new(Noon);
        using TaskToolHarness harness = new(clock: clock, proof: new FixedProofSource(FixedProofSource.Complete));
        using TempDataDir dir = new();
        TaskItem task = AddGatedTask(harness, "Kai", gated: true, accepted: true, TaskState.NeedsYou);
        ProcessMergeGate gate = Build(harness, dir, AcceptGate, clock);

        clock.Advance(TimeSpan.FromHours(24) + TimeSpan.FromTicks(1));

        string? refusal = gate.RefusalFor("Nova", FeatureMerge);
        Assert.StartsWith($"Cannot merge: the Human's Accept on Task {task.Id} is more than 24 hours old. Ask for a fresh one: move the Task to Needs You again with update_task", refusal, StringComparison.Ordinal); // contains-ok: the tail is the same wording as the other refusals.
    }

    /// <summary>A closed Task's Accept does not carry a merge.</summary>
    [Fact]
    public void RefusalFor_AcceptOnClosedTask_Refuses()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        TaskItem task = AddGatedTask(harness, "Kai", gated: true, accepted: true, TaskState.NeedsYou);
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);
        Assert.Null(gate.RefusalFor("Nova", FeatureMerge));

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Close(task.Id, TaskActors.Human(harness.Options.Value)));

        Assert.NotNull(gate.RefusalFor("Nova", FeatureMerge));
    }

    /// <summary>Both gates on: an Accept that took the Task straight to Done lets the merge through the checks gate too, on that Task's own Evidence.</summary>
    [Fact]
    public void RefusalFor_AcceptStraightToDoneWithEvidence_PassesBothGates()
    {
        ManualTimeProvider clock = new(Noon);
        using TaskToolHarness harness = new(clock: clock, proof: new FixedProofSource(FixedProofSource.Complete));
        using TempDataDir dir = new();
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskActor nova = new(TaskActorKind.Agent, "Nova", "id");
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Quote calculator", Team, null, Assignee: "Nova") { HumanGate = true }, human)).Task;
        _ = harness.Service.AddEvidence(task.Id, new EvidenceLine(Noon, "Nova", "dotnet test", 0, null, null, string.Empty), nova);
        task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, nova)).Task;
        task = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, human)).Task;
        Assert.Equal(TaskState.Done, task.Status);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge', 'accept-before-merge'", clock);

        Assert.Null(gate.RefusalFor("Nova", FeatureMerge));
    }

    /// <summary>An Accept that left the Task open with an Action, followed by an Agent's move to Done within the hour, does not open the one-hour window.</summary>
    [Fact]
    public void RefusalFor_AcceptWithActionThenAgentDone_DoesNotOpenTheWindow()
    {
        ManualTimeProvider clock = new(Noon);
        using TaskToolHarness harness = new(clock: clock, proof: new FixedProofSource(FixedProofSource.Complete));
        using TempDataDir dir = new();
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskActor nova = new(TaskActorKind.Agent, "Nova", "id");
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Quote calculator", Team, null, Assignee: "Nova") { HumanGate = true, Action = "Merge it" }, human)).Task;
        task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, nova)).Task;
        task = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, human)).Task;
        clock.Advance(TimeSpan.FromMinutes(5));
        task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, nova)).Task;
        Assert.Equal(TaskState.Done, task.Status);
        ProcessMergeGate gate = Build(harness, dir, AcceptGate, clock);

        Assert.NotNull(gate.RefusalFor("Nova", FeatureMerge));
    }

    /// <summary>A Human setting the Action on a Task in Review does not move it to Needs You; only an Agent's write does.</summary>
    [Fact]
    public void Update_HumanSetsAction_DoesNotMoveToNeedsYou()
    {
        using TaskToolHarness harness = NewHarness();
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Quote calculator", Team, null, TaskState.InProgress, Assignee: "Nova"), human)).Task;

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Action = Optional<string?>.Set("Merge it") }, null, human));

        Assert.Equal(TaskState.InProgress, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A Team without <c>accept-before-merge</c> on its gates line holds a merge into main for an Accept all the same: with no Accept it is refused, and an Accept on a Task of the Team lets it through.</summary>
    [Theory]
    [InlineData("'qa-evidence-before-done'")]
    [InlineData("")]
    public void RefusalFor_NoAcceptGateEntry_StillHoldsMergeAndPushToMain(string gates)
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, gates);

        Assert.NotNull(gate.RefusalFor("Nova", FeatureMerge));
        Assert.NotNull(gate.RefusalFor("Nova", "git merge feat/x"));
        Assert.NotNull(gate.RefusalFor("Nova", "git push origin main"));

        _ = AddGatedTask(harness, "Kai", gated: true, accepted: true, TaskState.NeedsYou);

        Assert.Null(gate.RefusalFor("Nova", FeatureMerge));
    }

    /// <summary>A fresh Accept on a Task whose Action names a push passes a push of a feature branch, and the same Accept passes a merge too.</summary>
    [Fact]
    public void RefusalFor_FreshAcceptNamingAPush_PassesThePush()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);
        Assert.NotNull(gate.RefusalFor("Nova", "git push origin feat/x"));

        _ = AddAcceptedTask(harness, "Push feat/x to origin");

        Assert.Null(gate.RefusalFor("Nova", "git push origin feat/x"));
        Assert.Null(gate.RefusalFor("Nova", "git push https://host/r.git feat/x"));
    }

    /// <summary>An Accept on a Task whose Action is Huddle's own merge Action, or any Action that does not name a push, does not pass a push.</summary>
    [Theory]
    [InlineData("Merge this Task's work into main")]
    [InlineData("Merge feat/x into main")]
    [InlineData("Pushing is not here, only a pushover")]
    public void RefusalFor_FreshAcceptNotNamingAPush_RefusesThePush(string action)
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, AcceptGate);
        _ = AddAcceptedTask(harness, action);

        Assert.Null(gate.RefusalFor("Nova", FeatureMerge));
        Assert.StartsWith("Cannot push: a push leaves the Team's repository", gate.RefusalFor("Nova", "git push origin feat/x"), StringComparison.Ordinal); // contains-ok: the full wording is pinned in NeedsYouLevelMergeGateTests.
    }

    /// <summary>An Accept naming a push that is more than 24 hours old no longer passes it, and the refusal says so.</summary>
    [Fact]
    public void RefusalFor_AcceptNamingAPushOlderThanADay_IsRefusedAsOld()
    {
        ManualTimeProvider clock = new(Noon);
        using TaskToolHarness harness = new(clock: clock, proof: new FixedProofSource(FixedProofSource.Complete));
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, AcceptGate, clock);
        TaskItem task = AddAcceptedTask(harness, "Push feat/x to origin");
        clock.Advance(TimeSpan.FromHours(24));
        Assert.Null(gate.RefusalFor("Nova", "git push origin feat/x"));

        clock.Advance(TimeSpan.FromTicks(1));

        Assert.StartsWith($"Cannot push: the Human's Accept on Task {task.Id} is more than 24 hours old.", gate.RefusalFor("Nova", "git push origin feat/x"), StringComparison.Ordinal); // contains-ok: the tail repeats the merge wording.
    }

    private static TaskItem AddAcceptedTask(TaskToolHarness harness, string action)
    {
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskDraft draft = new("Quote calculator", Team, null, Assignee: "Kai", Description: "Body") { HumanGate = true, Action = action };
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, human)).Task;
        task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, new TaskActor(TaskActorKind.Agent, "Kai", "id"))).Task;
        return Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, human)).Task;
    }

    private static TaskItem AddGatedTask(TaskToolHarness harness, string assignee, bool gated, bool accepted, TaskState status)
    {
        TaskDraft draft = new("Quote calculator", Team, null, Assignee: assignee, Description: "Body") { HumanGate = gated, Action = gated ? "Merge it" : null };
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, human)).Task;
        if (accepted)
        {
            task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, new TaskActor(TaskActorKind.Agent, assignee, "id"))).Task;
            task = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, human)).Task;
        }
        else if (status == TaskState.Cancelled)
        {
            task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Cancelled }, null, human)).Task;
        }

        return task;
    }

    private static TaskToolHarness NewHarness() => new(clock: new ManualTimeProvider(Noon), proof: new FixedProofSource(FixedProofSource.Complete));

    private static void AddEvidence(TaskToolHarness harness, string command, int exitCode, int minutes = 0)
    {
        TaskResult created = harness.Service.Create(new TaskDraft("Fix login", Team, null), TaskActors.Human(harness.Options.Value));
        TaskItem task = Assert.IsType<TaskResult.Saved>(created).Task;
        EvidenceLine line = new(Noon.AddMinutes(minutes), "Nova", command, exitCode, null, null, string.Empty);
        TaskResult added = harness.Service.AddEvidence(task.Id, line, new TaskActor(TaskActorKind.Agent, "Nova", "nova-id"));
        Assert.IsType<TaskResult.Saved>(added);
    }

    private static ProcessMergeGate Build(TaskToolHarness harness, TempDataDir dir, string gates, TimeProvider? clock = null)
    {
        VersionedRef type = new("web", "1.2.0");
        LibraryRoot root = new("teams", "Teams", dir.Path, LibraryRootKind.Teams);
        Assert.True(TeamFile.TryParse($"---\nteamType: 'web@1.2.0'\ngates: [{gates}]\ncheckCommand: 'dotnet test'\n---\n", out TeamFile? teamFile));
        FakeTeamFolders folders = new()
        {
            TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, dir.Path, LibraryNodeRole.TeamFolder)),
            ReadResult = teamFile,
        };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], [], true) { TeamType = type }] };
        ProcessGateService service = new(catalog, folders, RoleFixtures.Types(dir));
        return new ProcessMergeGate(harness.Personas, harness.Store, service, catalog, clock ?? new ManualTimeProvider(Noon));
    }
}
