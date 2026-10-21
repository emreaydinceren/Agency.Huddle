using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>The pure rule that turns one Task change into Feed items and resolutions (Feed spec §6.5.1).</summary>
public sealed class TaskFeedRulesTests
{
    private const string HumanName = "You";

    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");
    private static readonly TaskActor Human = new(TaskActorKind.Human, HumanName, "human");

    private static readonly Func<string, bool> NoConfirmer = static _ => false;
    private static readonly Func<string, bool> HasConfirmer = static _ => true;

    private static readonly TaskActor Helper = TaskActors.Helper("Needs Human");

    private static FeedEffects Run(TaskItem? before, TaskItem after, TaskActor actor, Func<string, bool>? confirmer = null, string? reason = null, TaskDecision? decision = null) =>
        TaskFeedRules.For(Change(before, after, actor, reason, decision), HumanName, confirmer ?? NoConfirmer, static _ => new Proof([new ProofItem("a", true, null, null, ProofItemSource.Process), new ProofItem("b", false, null, null, ProofItemSource.Process)]));

    private static TaskChange Change(TaskItem? before, TaskItem after, TaskActor actor, string? reason = null, TaskDecision? decision = null) =>
        new(before, after, [], actor, new ChangeLogEntry(DateTimeOffset.UnixEpoch, actor.Name, "x"), decision, null, reason);

    private static TaskItem Task(TaskState status = TaskState.ToDo, string? assignee = null, bool gated = false, IReadOnlyList<KeyValuePair<string, string>>? extra = null) =>
        TestTasks.Make(id: "PLAT-0001", title: "Fix footer", status: status, assignee: assignee, unknownFields: extra) with { HumanGate = gated };

    private static List<KeyValuePair<string, string>> RequestFrom(string fromTeam, string confirmedBy = "", TriageState triage = TriageState.Accepted) =>
        [.. new RequestFields(RequestKind.Bug, fromTeam, "Dana", triage, string.Empty, confirmedBy).ToFrontmatter()];

    /// <summary>A creation posts one non-Notification item naming the Task, whoever created it.</summary>
    [Fact]
    public void For_Creation_PostsTaskCreatedActivityForAnyActor()
    {
        FeedEffects byAgent = Run(null, Task(), Nova);
        FeedEffects byHuman = Run(null, Task(), Human);

        FeedMessage message = Assert.Single(byAgent.Posts);
        Assert.Equal(FeedSourceType.TaskCreated, message.SourceType);
        Assert.False(message.Notify);
        Assert.Equal("PLAT-0001", message.TargetKey);
        Assert.Equal(new FeedActor(FeedActorKind.Agent, "Nova"), message.Actor);
        Assert.Single(byHuman.Posts);
        Assert.Equal(FeedActorKind.Human, byHuman.Posts[0].Actor.Kind);
    }

    /// <summary>A Task created already assigned to the Human by an Agent is also a Notification; by the Human it is not.</summary>
    [Fact]
    public void For_CreatedAssignedToHuman_NotifiesOnlyWhenSomeoneElseDidIt()
    {
        FeedEffects byAgent = Run(null, Task(assignee: HumanName), Nova);
        FeedEffects byHuman = Run(null, Task(assignee: HumanName), Human);

        Assert.Contains(byAgent.Posts, post => post.SourceType == FeedSourceType.TaskAssignedToYou && post.Notify);
        Assert.DoesNotContain(byHuman.Posts, post => post.Notify);
    }

    /// <summary>Turning the gate on posts activity for an Agent and nothing for the Human.</summary>
    [Fact]
    public void For_GateTurnedOn_PostsActivityUnlessHumanDidIt()
    {
        TaskItem before = Task();
        TaskItem after = before with { HumanGate = true };

        FeedEffects byAgent = Run(before, after, Nova);
        FeedEffects byHuman = Run(before, after, Human);

        FeedMessage post = Assert.Single(byAgent.Posts);
        Assert.Equal(FeedSourceType.TaskGateOn, post.SourceType);
        Assert.False(post.Notify);
        Assert.Empty(byHuman.Posts);
    }

    /// <summary>A Helper's gate-on is a Notification that carries the reason and the Helper actor; a Teammate's stays Activity with no reason.</summary>
    [Fact]
    public void For_GateTurnedOnByHelper_NotifiesWithTheReason()
    {
        TaskItem before = Task();
        TaskItem after = before with { HumanGate = true };

        FeedEffects byHelper = Run(before, after, Helper, reason: "Sends a document outside the Team");
        FeedEffects byAgent = Run(before, after, Nova, reason: "ignored");

        FeedMessage post = Assert.Single(byHelper.Posts);
        Assert.Equal(FeedSourceType.TaskGateOn, post.SourceType);
        Assert.True(post.Notify);
        Assert.Equal(new FeedActor(FeedActorKind.Helper, "Needs Human (Helper)"), post.Actor);
        TaskGatePayload payload = Assert.IsType<TaskGatePayload>(post.Payload);
        Assert.Equal("Sends a document outside the Team", payload.Reason);
        FeedMessage teammate = Assert.Single(byAgent.Posts);
        Assert.False(teammate.Notify);
        Assert.Null(Assert.IsType<TaskGatePayload>(teammate.Payload).Reason);
    }

    /// <summary>Every kind of Task actor maps to its own Feed actor kind; none falls into Outside Huddle by default.</summary>
    [Fact]
    public void For_EachActorKind_PostsItsOwnFeedActorKind()
    {
        Dictionary<TaskActorKind, FeedActorKind> expected = new()
        {
            [TaskActorKind.Human] = FeedActorKind.Human,
            [TaskActorKind.Agent] = FeedActorKind.Agent,
            [TaskActorKind.Helper] = FeedActorKind.Helper,
            [TaskActorKind.OutsideHuddle] = FeedActorKind.OutsideHuddle,
        };

        foreach (TaskActorKind kind in Enum.GetValues<TaskActorKind>())
        {
            Assert.True(expected.ContainsKey(kind), $"{kind} needs an explicit Feed actor kind.");
            Assert.Equal(expected[kind], Run(null, Task(), new TaskActor(kind, "Someone", null)).Posts[0].Actor.Kind);
        }
    }

    /// <summary>A Helper's gate-on Notification resolves when the gate goes off, a decision is recorded, the Task starts waiting in Needs You, or it closes.</summary>
    [Fact]
    public void For_HelperGateOn_ResolvesOnEachOfTheFourConditions()
    {
        FeedResolution gateOn = new("PLAT-0001", FeedSourceType.TaskGateOn);
        TaskItem gated = Task(TaskState.InProgress, gated: true);

        FeedEffects gateOff = Run(gated, gated with { HumanGate = false }, Human);
        FeedEffects decided = Run(gated, gated with { Status = TaskState.InProgress }, Human, decision: TaskDecision.RequestChanges);
        FeedEffects waits = Run(gated, gated with { Status = TaskState.NeedsYou }, Nova);
        FeedEffects closed = Run(gated, gated with { Location = gated.Location with { Closed = true } }, Human);
        FeedEffects unrelated = Run(gated, gated with { Title = "New" }, Nova);

        Assert.Contains(gateOn, gateOff.Resolutions);
        Assert.Contains(gateOn, decided.Resolutions);
        Assert.Contains(gateOn, waits.Resolutions);
        Assert.Contains(gateOn, closed.Resolutions);
        Assert.DoesNotContain(gateOn, unrelated.Resolutions);
    }

    /// <summary>A gate-on item still holds while the Task is open, gated, undecided and not in Needs You; each of those ends it.</summary>
    [Fact]
    public void Holds_TaskGateOn_ReflectsEachConditionNow()
    {
        TaskItem gated = Task(TaskState.InProgress, gated: true) with
        {
            ChangeLog = [new ChangeLogEntry(DateTimeOffset.UnixEpoch, "Needs Human (Helper)", "human gate on (reason: x)")],
        };
        TaskItem decided = gated with { ChangeLog = [.. gated.ChangeLog, new ChangeLogEntry(DateTimeOffset.UnixEpoch, HumanName, "changes requested: redo")] };
        TaskItem accepted = gated with { ChangeLog = [.. gated.ChangeLog, new ChangeLogEntry(DateTimeOffset.UnixEpoch, HumanName, "accepted abcdef123456")] };

        Assert.True(TaskFeedRules.Holds(FeedSourceType.TaskGateOn, gated, HumanName, NoConfirmer, null));
        Assert.False(TaskFeedRules.Holds(FeedSourceType.TaskGateOn, gated with { HumanGate = false }, HumanName, NoConfirmer, null));
        Assert.False(TaskFeedRules.Holds(FeedSourceType.TaskGateOn, gated with { Status = TaskState.NeedsYou }, HumanName, NoConfirmer, null));
        Assert.False(TaskFeedRules.Holds(FeedSourceType.TaskGateOn, gated with { Location = gated.Location with { Closed = true } }, HumanName, NoConfirmer, null));
        Assert.False(TaskFeedRules.Holds(FeedSourceType.TaskGateOn, decided, HumanName, NoConfirmer, null));
        Assert.False(TaskFeedRules.Holds(FeedSourceType.TaskGateOn, accepted, HumanName, NoConfirmer, null));
    }

    /// <summary>A gated Task entering Needs You posts a Notification with its Proof count; the Human's own move posts none.</summary>
    [Fact]
    public void For_GatedTaskMovesToNeedsYou_NotifiesWithProof()
    {
        TaskItem before = Task(TaskState.InProgress, gated: true);
        TaskItem after = before with { Status = TaskState.NeedsYou };

        FeedEffects byAgent = Run(before, after, Nova);
        FeedEffects byHuman = Run(before, after, Human);

        FeedMessage post = Assert.Single(byAgent.Posts);
        Assert.Equal(FeedSourceType.TaskNeedsYou, post.SourceType);
        Assert.True(post.Notify);
        TaskNeedsYouPayload payload = Assert.IsType<TaskNeedsYouPayload>(post.Payload);
        Assert.Equal((1, 2), (payload.ProofMet, payload.ProofTotal));
        Assert.Empty(byHuman.Posts);
    }

    /// <summary>Leaving Needs You (accepted, moved on) resolves the Needs You Notification.</summary>
    [Fact]
    public void For_GatedTaskLeavesNeedsYou_ResolvesIt()
    {
        TaskItem before = Task(TaskState.NeedsYou, gated: true);
        TaskItem after = before with { Status = TaskState.InProgress };

        FeedEffects effects = Run(before, after, Human);

        Assert.Contains(new FeedResolution("PLAT-0001", FeedSourceType.TaskNeedsYou), effects.Resolutions);
    }

    /// <summary>Closing a gated Task that is still in Needs You resolves its Notification even though the status did not change.</summary>
    [Fact]
    public void For_GatedTaskClosedInNeedsYou_ResolvesIt()
    {
        TaskItem before = Task(TaskState.NeedsYou, gated: true);
        TaskItem after = before with { Location = before.Location with { Closed = true } };

        FeedEffects effects = Run(before, after, Human);

        Assert.Contains(new FeedResolution("PLAT-0001", FeedSourceType.TaskNeedsYou), effects.Resolutions);
    }

    /// <summary>An ungated Task entering Needs You is not waiting on an acceptance, so nothing is posted.</summary>
    [Fact]
    public void For_UngatedTaskMovesToNeedsYou_PostsNothing()
    {
        TaskItem before = Task(TaskState.InProgress);

        FeedEffects effects = Run(before, before with { Status = TaskState.NeedsYou }, Nova);

        Assert.Empty(effects.Posts);
    }

    /// <summary>Assigning an open Task to the Human notifies; reassigning it away resolves; a terminal status resolves.</summary>
    [Fact]
    public void For_AssignedToHuman_NotifiesAndResolvesWhenItLeaves()
    {
        TaskItem unassigned = Task();
        TaskItem assigned = unassigned with { Assignee = HumanName };

        FeedEffects assign = Run(unassigned, assigned, Nova);
        FeedEffects reassign = Run(assigned, assigned with { Assignee = "Kai" }, Nova);
        FeedEffects done = Run(assigned, assigned with { Status = TaskState.Done }, Human);

        FeedMessage post = Assert.Single(assign.Posts);
        Assert.Equal(FeedSourceType.TaskAssignedToYou, post.SourceType);
        Assert.True(post.Notify);
        Assert.Contains(new FeedResolution("PLAT-0001", FeedSourceType.TaskAssignedToYou), reassign.Resolutions);
        Assert.Contains(new FeedResolution("PLAT-0001", FeedSourceType.TaskAssignedToYou), done.Resolutions);
    }

    /// <summary>The Human assigning a Task to themself posts no Notification.</summary>
    [Fact]
    public void For_HumanAssignsToSelf_PostsNothing()
    {
        TaskItem before = Task();

        FeedEffects effects = Run(before, before with { Assignee = HumanName }, Human);

        Assert.Empty(effects.Posts);
    }

    /// <summary>A Request reaching Done with no confirmer on the requesting Team notifies; with a confirmer it posts nothing.</summary>
    [Fact]
    public void For_RequestDone_NotifiesOnlyWhenNoAgentCanConfirm()
    {
        TaskItem before = Task(TaskState.InProgress, extra: RequestFrom("Design"));
        TaskItem after = before with { Status = TaskState.Done };

        FeedEffects alone = Run(before, after, Nova, NoConfirmer);
        FeedEffects withQa = Run(before, after, Nova, HasConfirmer);

        FeedMessage post = Assert.Single(alone.Posts);
        Assert.Equal(FeedSourceType.RequestAwaitsYou, post.SourceType);
        RequestAwaitsPayload payload = Assert.IsType<RequestAwaitsPayload>(post.Payload);
        Assert.Equal(("Design", "bug"), (payload.FromTeam, payload.Kind));
        Assert.Empty(withQa.Posts);
    }

    /// <summary>A declined or already confirmed Request is not waiting on the Human, and closing or reopening resolves the ask.</summary>
    [Fact]
    public void For_RequestDeclinedConfirmedOrClosed_DoesNotNotifyAndResolves()
    {
        TaskItem declined = Task(TaskState.Done, extra: RequestFrom("Design", triage: TriageState.Declined));
        TaskItem confirmed = Task(TaskState.Done, extra: RequestFrom("Design", confirmedBy: "Quinn"));
        TaskItem waiting = Task(TaskState.Done, extra: RequestFrom("Design"));

        FeedEffects fromDeclined = Run(Task(TaskState.InProgress, extra: RequestFrom("Design", triage: TriageState.Declined)), declined, Nova);
        FeedEffects fromConfirmed = Run(Task(TaskState.InProgress, extra: RequestFrom("Design")), confirmed, Nova);
        FeedEffects closed = Run(waiting, waiting with { Location = waiting.Location with { Closed = true } }, Human);

        Assert.Empty(fromDeclined.Posts);
        Assert.Empty(fromConfirmed.Posts);
        Assert.Contains(new FeedResolution("PLAT-0001", FeedSourceType.RequestAwaitsYou), closed.Resolutions);
    }

    /// <summary>A status change or a closing resolves a gate-decision Notification; a change that keeps the status does not.</summary>
    [Fact]
    public void For_StatusChangeOrClose_ResolvesGateDecision()
    {
        TaskItem before = Task(TaskState.InProgress);

        FeedEffects moved = Run(before, before with { Status = TaskState.Review }, Human);
        FeedEffects retitled = Run(before, before with { Title = "New" }, Human);

        Assert.Contains(new FeedResolution("PLAT-0001", FeedSourceType.GateDecisionDue), moved.Resolutions);
        Assert.DoesNotContain(retitled.Resolutions, resolution => resolution.Type == FeedSourceType.GateDecisionDue);
    }

    /// <summary>The startup check says a Notification still holds while its condition does, and not once the Task has moved on.</summary>
    [Fact]
    public void Holds_ReflectsEachConditionNow()
    {
        TaskItem waiting = Task(TaskState.NeedsYou, gated: true);
        GateDecisionPayload decision = new("PLAT-0001", "Fix footer", "Platform", "qa-evidence-before-done", "No.", TaskState.InProgress.ToWire(), TaskState.Done.ToWire());
        TaskItem stillThere = Task(TaskState.InProgress);

        Assert.True(TaskFeedRules.Holds(FeedSourceType.TaskNeedsYou, waiting, HumanName, NoConfirmer, null));
        Assert.False(TaskFeedRules.Holds(FeedSourceType.TaskNeedsYou, waiting with { Status = TaskState.InProgress }, HumanName, NoConfirmer, null));
        Assert.True(TaskFeedRules.Holds(FeedSourceType.TaskAssignedToYou, Task(assignee: HumanName), HumanName, NoConfirmer, null));
        Assert.False(TaskFeedRules.Holds(FeedSourceType.TaskAssignedToYou, Task(assignee: "Kai"), HumanName, NoConfirmer, null));
        Assert.True(TaskFeedRules.Holds(FeedSourceType.GateDecisionDue, stillThere, HumanName, NoConfirmer, decision));
        Assert.False(TaskFeedRules.Holds(FeedSourceType.GateDecisionDue, stillThere with { Status = TaskState.Review }, HumanName, NoConfirmer, decision));
        Assert.True(TaskFeedRules.Holds(FeedSourceType.RequestAwaitsYou, Task(TaskState.Done, extra: RequestFrom("Design")), HumanName, NoConfirmer, null));
        Assert.False(TaskFeedRules.Holds(FeedSourceType.RequestAwaitsYou, Task(TaskState.Done, extra: RequestFrom("Design")), HumanName, HasConfirmer, null));
    }

    /// <summary>When the Helper's gate-on lands in the same change that makes the Task wait in Needs You, the Needs You Notification carries the ask and the gate-on is Activity with the reason, never a Notification born resolved.</summary>
    [Fact]
    public void For_HelperGateOnThatLandsInNeedsYou_IsActivityAndNotResolvedInTheSameBreath()
    {
        TaskItem before = Task(TaskState.InProgress);
        TaskItem after = before with { HumanGate = true, Status = TaskState.NeedsYou };

        FeedEffects effects = Run(before, after, Helper, reason: "Sends a document outside the Team");

        FeedMessage gateOn = Assert.Single(effects.Posts, static post => post.SourceType == FeedSourceType.TaskGateOn);
        Assert.False(gateOn.Notify);
        Assert.Equal("Sends a document outside the Team", Assert.IsType<TaskGatePayload>(gateOn.Payload).Reason);
        Assert.DoesNotContain(new FeedResolution("PLAT-0001", FeedSourceType.TaskGateOn), effects.Resolutions);
    }
}
