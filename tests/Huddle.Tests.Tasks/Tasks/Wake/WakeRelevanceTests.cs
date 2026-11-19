using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Tasks.Wake;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>
/// <see cref="WakeRelevance"/> says, from the fields and the kind of a change alone, whether it wakes the assignee and whether it
/// wakes the manager (Smart wakes spec 3.1 and 3.2, corrections 3 and 4). These tests pin every <see cref="TaskField"/> value, every
/// <see cref="TaskChangeKind"/> and the order of the checks. The status table has its own class, <see cref="WakeRelevanceStatusTests"/>.
/// </summary>
public sealed class WakeRelevanceTests
{
    /// <summary>The one field change a test builds for <paramref name="field"/>: a real status move for Status, any two values otherwise.</summary>
    private static FieldChange FieldOf(TaskField field) =>
        field == TaskField.Status ? new FieldChange(field, "In Progress", "Done") : new FieldChange(field, "before", "after");

    /// <summary>Whether <paramref name="change"/> wakes the assignee and whether it wakes the manager, in that order.</summary>
    private static (bool Assignee, bool Manager) Verdicts(TaskChange change) =>
        (WakeRelevance.Wakes(change, WakeRecipient.Assignee), WakeRelevance.Wakes(change, WakeRecipient.Manager));

    /// <summary>The same pair for a field list judged on its own.</summary>
    private static (bool Assignee, bool Manager) Verdicts(IReadOnlyList<FieldChange> changes, TaskActorKind kind) =>
        (WakeRelevance.Wakes(changes, kind, WakeRecipient.Assignee), WakeRelevance.Wakes(changes, kind, WakeRecipient.Manager));

    /// <summary>Asserts the verdicts, in order, whole.</summary>
    /// <param name="expected">The expected assignee and manager verdicts.</param>
    /// <param name="actual">The verdicts the rule gave.</param>
    private static void AssertVerdicts((bool Assignee, bool Manager)[] expected, params (bool Assignee, bool Manager)[] actual) =>
        Assert.Equal(expected, actual);

    /// <summary>Every field of 3.1 has its verdict, and a status move that is In Progress to Done wakes both: one row per <see cref="TaskField"/>.</summary>
    [Theory]
    [InlineData(TaskField.Title, true)]
    [InlineData(TaskField.Status, true)]
    [InlineData(TaskField.Priority, false)]
    [InlineData(TaskField.Assignee, true)]
    [InlineData(TaskField.Origin, false)]
    [InlineData(TaskField.Parent, false)]
    [InlineData(TaskField.BlockedBy, true)]
    [InlineData(TaskField.DuplicateOf, false)]
    [InlineData(TaskField.Tags, false)]
    [InlineData(TaskField.StartDate, false)]
    [InlineData(TaskField.DueDate, false)]
    [InlineData(TaskField.Sprint, false)]
    [InlineData(TaskField.Rank, false)]
    [InlineData(TaskField.HumanGate, false)]
    [InlineData(TaskField.Action, true)]
    [InlineData(TaskField.Description, true)]
    [InlineData(TaskField.Location, true)]
    [InlineData(TaskField.Unknown, true)]
    public void Wakes_FieldByTheHuman_FollowsTheTable(TaskField field, bool wakes)
    {
        Assert.Equal((wakes, wakes), Verdicts([FieldOf(field)], TaskActorKind.Human));
    }

    /// <summary>One list for every actor: an Agent, a Helper and the Human get the same verdict on every field except Location, so a new <see cref="TaskField"/> cannot go unclassified.</summary>
    [Fact]
    public void Wakes_EveryTaskField_HasTheSameVerdictForAgentHelperAndHuman()
    {
        Dictionary<TaskField, bool> expected = new()
        {
            [TaskField.Title] = true,
            [TaskField.Status] = true,
            [TaskField.Priority] = false,
            [TaskField.Assignee] = true,
            [TaskField.Origin] = false,
            [TaskField.Parent] = false,
            [TaskField.BlockedBy] = true,
            [TaskField.DuplicateOf] = false,
            [TaskField.Tags] = false,
            [TaskField.StartDate] = false,
            [TaskField.DueDate] = false,
            [TaskField.Sprint] = false,
            [TaskField.Rank] = false,
            [TaskField.HumanGate] = false,
            [TaskField.Action] = true,
            [TaskField.Description] = true,
            [TaskField.Location] = true,
            [TaskField.Unknown] = true,
        };

        foreach (TaskActorKind kind in new[] { TaskActorKind.Human, TaskActorKind.Agent, TaskActorKind.Helper })
        {
            Dictionary<TaskField, bool> actual = Enum.GetValues<TaskField>()
                .ToDictionary(static field => field, field => WakeRelevance.Wakes([FieldOf(field)], kind, WakeRecipient.Assignee));
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>A Location change wakes when made inside Huddle (the Human, an Agent, a Helper) and not when made outside it (D-4), for both recipients.</summary>
    [Theory]
    [InlineData(TaskActorKind.Human, true)]
    [InlineData(TaskActorKind.Agent, true)]
    [InlineData(TaskActorKind.Helper, true)]
    [InlineData(TaskActorKind.OutsideHuddle, false)]
    public void Wakes_Location_DependsOnWhetherTheActorIsOutsideHuddle(TaskActorKind kind, bool wakes)
    {
        FieldChange move = new(TaskField.Location, "Platform", "Design");

        Assert.Equal((wakes, wakes), Verdicts([move], kind));
        Assert.Equal((wakes, wakes), Verdicts(WakeChanges.Edit(kind, move)));
    }

    /// <summary>A list holding one silent and one waking field wakes, and the order does not matter.</summary>
    [Fact]
    public void Wakes_SilentFieldTogetherWithWakingField_Wakes()
    {
        FieldChange rank = new(TaskField.Rank, "2", "1");
        FieldChange title = new(TaskField.Title, "Old", "New");

        Assert.Equal((true, true), Verdicts([rank, title], TaskActorKind.Agent));
        Assert.Equal((true, true), Verdicts([title, rank], TaskActorKind.Agent));
    }

    /// <summary>A list of only silent fields (rank, priority, tags, dates, the Human gate) wakes no one, whoever made it, while adding a title edit to it wakes.</summary>
    [Fact]
    public void Wakes_OnlySilentFields_WakesNoOne()
    {
        FieldChange[] silent =
        [
            new(TaskField.Rank, "2", "1"),
            new(TaskField.Priority, "Low", "High"),
            new(TaskField.Tags, "a", "a, b"),
            new(TaskField.DueDate, null, "2026-12-01"),
            new(TaskField.HumanGate, "off", "on"),
        ];
        FieldChange[] withTitle = [.. silent, new FieldChange(TaskField.Title, "Old", "New")];

        AssertVerdicts(
            [(false, false), (false, false), (true, true)],
            Verdicts(silent, TaskActorKind.Human),
            Verdicts(silent, TaskActorKind.Agent),
            Verdicts(withTitle, TaskActorKind.Agent));
    }

    /// <summary>An empty field list wakes no one on its own, while a one-field list does.</summary>
    [Fact]
    public void Wakes_NoFields_WakesNoOne()
    {
        AssertVerdicts(
            [(false, false), (true, true)],
            Verdicts([], TaskActorKind.Agent),
            Verdicts([new FieldChange(TaskField.Description, "a", "b")], TaskActorKind.Agent));
    }

    /// <summary>A status field is judged from its old and new wire strings: To Do to In Progress wakes the assignee only, Backlog to Cancelled wakes no one.</summary>
    [Fact]
    public void Wakes_StatusField_IsJudgedByTheMove()
    {
        Assert.Equal((true, false), Verdicts([new FieldChange(TaskField.Status, "To Do", "In Progress")], TaskActorKind.Human));
        Assert.Equal((true, true), Verdicts([new FieldChange(TaskField.Status, "In Progress", "Cancelled")], TaskActorKind.Human));
        Assert.Equal((false, false), Verdicts([new FieldChange(TaskField.Status, "Backlog", "Cancelled")], TaskActorKind.Human));
    }

    /// <summary>A status value that does not parse wakes both recipients (correction 4): Huddle cannot judge a move it cannot read.</summary>
    [Theory]
    [InlineData("Nonsense", "Done")]
    [InlineData("Backlog", "Nonsense")]
    [InlineData(null, "Cancelled")]
    [InlineData("Backlog", null)]
    public void Wakes_StatusFieldThatDoesNotParse_Wakes(string? oldValue, string? newValue)
    {
        Assert.Equal((true, true), Verdicts([new FieldChange(TaskField.Status, oldValue, newValue)], TaskActorKind.Agent));
    }

    /// <summary>A status move in a change is judged from the field's old and new values, never from <c>Before.Status</c> and <c>After.Status</c> (correction 4).</summary>
    [Fact]
    public void Wakes_StatusMoveInAChange_IgnoresTheTasksOwnStatus()
    {
        TaskChange silentMove = WakeChanges.Make(
            WakeChanges.Task(TaskState.InProgress),
            WakeChanges.Task(TaskState.Done),
            [new FieldChange(TaskField.Status, "Backlog", "Cancelled")]);
        TaskChange wakingMove = WakeChanges.Make(
            WakeChanges.Task(TaskState.Backlog),
            WakeChanges.Task(TaskState.Backlog),
            [new FieldChange(TaskField.Status, "In Progress", "Done")]);

        Assert.Equal((false, false), Verdicts(silentMove));
        Assert.Equal((true, true), Verdicts(wakingMove));
    }

    /// <summary>A change whose Before is null is a Create and wakes both recipients whatever its Kind and whatever its fields.</summary>
    [Fact]
    public void Wakes_BeforeNull_IsACreateWhateverTheKind()
    {
        TaskChange kindEdit = WakeChanges.Make(null, WakeChanges.Task(), [], TaskActorKind.OutsideHuddle);
        TaskChange kindCreate = WakeChanges.Make(null, WakeChanges.Task(), [], TaskActorKind.Human, TaskChangeKind.Create);
        TaskChange kindHuddleStep = WakeChanges.Make(null, WakeChanges.Task(), [], TaskActorKind.Helper, TaskChangeKind.HuddleStep);

        Assert.Equal((true, true), Verdicts(kindEdit));
        Assert.Equal((true, true), Verdicts(kindCreate));
        Assert.Equal((true, true), Verdicts(kindHuddleStep));
    }

    /// <summary>A Create raised by <c>WriteNewTask</c> (Kind Create, Before given or not) wakes both recipients.</summary>
    [Fact]
    public void Wakes_KindCreate_WakesBoth()
    {
        Assert.Equal((true, true), Verdicts(WakeChanges.Bare(TaskChangeKind.Create, TaskState.ToDo)));
    }

    /// <summary>Evidence wakes both recipients: the manager checks it.</summary>
    [Fact]
    public void Wakes_KindEvidence_WakesBoth()
    {
        Assert.Equal((true, true), Verdicts(WakeChanges.Bare(TaskChangeKind.Evidence)));
    }

    /// <summary>A release (merge allowed without an Accept) wakes both recipients: it carries the "merge now" sentence.</summary>
    [Fact]
    public void Wakes_KindRelease_WakesBoth()
    {
        Assert.Equal((true, true), Verdicts(WakeChanges.Bare(TaskChangeKind.Release)));
    }

    /// <summary>Huddle's Sprint step is a log line only and wakes no one, though Evidence on the same Task does.</summary>
    [Fact]
    public void Wakes_KindHuddleStep_WakesNoOne()
    {
        AssertVerdicts(
            [(false, false), (true, true)],
            Verdicts(WakeChanges.Bare(TaskChangeKind.HuddleStep)),
            Verdicts(WakeChanges.Bare(TaskChangeKind.Evidence)));
    }

    /// <summary>In slice 1 a comment still wakes exactly as today (correction 11): <c>Wakes</c> is true for the Comment kind, for both recipients.</summary>
    [Fact]
    public void Wakes_KindComment_StillWakesBothInThisSlice()
    {
        TaskChange comment = WakeChanges.Make(WakeChanges.Task(), WakeChanges.Task(), [], TaskActorKind.Agent, TaskChangeKind.Comment, comment: "Looks good.");

        Assert.Equal((true, true), Verdicts(comment));
    }

    /// <summary>An edit with an empty field list and no other sign (a gate's refusal, a reset creation, a forged Accept) is silent, while the same Task with a Release kind wakes.</summary>
    [Fact]
    public void Wakes_KindEditWithNoFields_IsSilent()
    {
        TaskChange refusal = WakeChanges.Make(WakeChanges.Task(TaskState.InProgress), WakeChanges.Task(TaskState.InProgress), [], TaskActorKind.OutsideHuddle);
        TaskChange release = WakeChanges.Make(WakeChanges.Task(TaskState.InProgress), WakeChanges.Task(TaskState.InProgress), [], TaskActorKind.OutsideHuddle, TaskChangeKind.Release);

        AssertVerdicts([(false, false), (true, true)], Verdicts(refusal), Verdicts(release));
    }

    /// <summary>Closing or reopening wakes both recipients only when the status is In Progress or Needs You, and a finished, To Do or Backlog Task files away silently.</summary>
    [Theory]
    [InlineData(TaskState.Backlog, false)]
    [InlineData(TaskState.ToDo, false)]
    [InlineData(TaskState.InProgress, true)]
    [InlineData(TaskState.NeedsYou, true)]
    [InlineData(TaskState.Done, false)]
    [InlineData(TaskState.Cancelled, false)]
    [InlineData(TaskState.Duplicate, false)]
    [InlineData(TaskState.Rejected, false)]
    public void Wakes_CloseAndReopen_WakeOnlyActiveTasks(TaskState status, bool wakes)
    {
        Assert.Equal((wakes, wakes), Verdicts(WakeChanges.CloseOrReopen(status, closing: true, TaskChangeKind.Close)));
        Assert.Equal((wakes, wakes), Verdicts(WakeChanges.CloseOrReopen(status, closing: false, TaskChangeKind.Reopen)));
    }

    /// <summary>A change that flips the closed flag is a Close or Reopen whatever its Kind says (correction 3): an outside move into or out of the closed folder is raised as an Edit.</summary>
    [Theory]
    [InlineData(TaskState.InProgress, true)]
    [InlineData(TaskState.NeedsYou, true)]
    [InlineData(TaskState.ToDo, false)]
    [InlineData(TaskState.Done, false)]
    public void Wakes_ClosedFlagFlippedByAnEdit_IsJudgedAsCloseOrReopen(TaskState status, bool wakes)
    {
        Assert.Equal((wakes, wakes), Verdicts(WakeChanges.CloseOrReopen(status, closing: true, TaskChangeKind.Edit, TaskActorKind.OutsideHuddle)));
        Assert.Equal((wakes, wakes), Verdicts(WakeChanges.CloseOrReopen(status, closing: false, TaskChangeKind.Edit, TaskActorKind.OutsideHuddle)));
    }

    /// <summary>
    /// Slice 1b ruling: when the closed flag flips under a Kind of Edit and the change also carries waking fields, it wakes whatever the status:
    /// the result is the Close/Reopen rule (Active only) OR the fields. A Title edit made together with a move into the closed folder of a To Do
    /// Task wakes, while the same flip with only silent fields does not.
    /// </summary>
    [Theory]
    [InlineData(TaskState.ToDo, "Title", true)]
    [InlineData(TaskState.Done, "Title", true)]
    [InlineData(TaskState.Backlog, "Description", true)]
    [InlineData(TaskState.ToDo, "Rank", false)]
    [InlineData(TaskState.Done, "Priority", false)]
    public void Wakes_ClosedFlagFlippedByAnEditThatAlsoCarriesFields_WakesWhenAFieldWakes(TaskState status, string fieldName, bool wakes)
    {
        TaskField field = Enum.Parse<TaskField>(fieldName);
        FieldChange[] changes = [new FieldChange(field, "before", "after")];

        Assert.Equal((wakes, wakes), Verdicts(WakeChanges.Make(WakeChanges.Task(status, closed: false), WakeChanges.Task(status, closed: true), changes, TaskActorKind.Agent)));
        Assert.Equal((wakes, wakes), Verdicts(WakeChanges.Make(WakeChanges.Task(status, closed: true), WakeChanges.Task(status, closed: false), changes, TaskActorKind.Agent)));
    }

    /// <summary>A pure Close or Reopen kind stays decided by the Task's status alone, even when it carries a waking field: only a Kind of Edit adds the fields.</summary>
    [Fact]
    public void Wakes_ClosedFlagFlippedByCloseKindWithAWakingField_StaysDecidedByTheStatus()
    {
        FieldChange[] title = [new FieldChange(TaskField.Title, "Old", "New")];
        TaskChange close = WakeChanges.Make(WakeChanges.Task(TaskState.ToDo, closed: false), WakeChanges.Task(TaskState.ToDo, closed: true), title, TaskActorKind.Agent, TaskChangeKind.Close);
        TaskChange reopen = WakeChanges.Make(WakeChanges.Task(TaskState.ToDo, closed: true), WakeChanges.Task(TaskState.ToDo, closed: false), title, TaskActorKind.Agent, TaskChangeKind.Reopen);
        TaskChange edit = WakeChanges.Make(WakeChanges.Task(TaskState.ToDo, closed: false), WakeChanges.Task(TaskState.ToDo, closed: true), title, TaskActorKind.Agent, TaskChangeKind.Edit);

        AssertVerdicts([(false, false), (false, false), (true, true)], Verdicts(close), Verdicts(reopen), Verdicts(edit));
    }

    /// <summary>A closed-flag flip by an Edit judges the fields per recipient: a move into the closed folder of a To Do Task with a To Do to In Progress status field wakes the assignee only.</summary>
    [Fact]
    public void Wakes_ClosedFlagFlippedByAnEditWithAStatusField_JudgesTheMovePerRecipient()
    {
        TaskChange change = WakeChanges.Make(
            WakeChanges.Task(TaskState.ToDo, closed: false),
            WakeChanges.Task(TaskState.ToDo, closed: true),
            [new FieldChange(TaskField.Status, "To Do", "In Progress")],
            TaskActorKind.Human);

        Assert.Equal((true, false), Verdicts(change));
    }

    /// <summary>A Human decision with no fields (accept, changes requested, decline) wakes both recipients, as it carries the decision sentence.</summary>
    [Theory]
    [InlineData(TaskDecision.Accept)]
    [InlineData(TaskDecision.RequestChanges)]
    [InlineData(TaskDecision.Decline)]
    public void Wakes_DecisionWithNoFields_WakesBoth(TaskDecision decision)
    {
        TaskChange change = WakeChanges.Make(WakeChanges.Task(TaskState.NeedsYou), WakeChanges.Task(TaskState.NeedsYou), [], TaskActorKind.Human, decision: decision);

        Assert.Equal((true, true), Verdicts(change));
    }

    /// <summary>
    /// A Decline closes the Task (the closed flag flips, the status is Rejected, which a close alone would file away silently) and still
    /// wakes both recipients, because the decision is checked before the closed-flag rule (correction 15). The Before and After Tasks are
    /// both Rejected so that reading either status for the close rule would be silent.
    /// </summary>
    /// <param name="raisedAsClose"><see langword="false"/> for the kind <c>DeclineCore</c> raises (Edit), <see langword="true"/> for a Close, which must judge the same.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Wakes_DeclineThatClosesTheTask_WakesBothBeforeTheClosedFlagIsJudged(bool raisedAsClose)
    {
        TaskChangeKind kind = raisedAsClose ? TaskChangeKind.Close : TaskChangeKind.Edit;
        TaskChange decline = WakeChanges.Make(
            WakeChanges.Task(TaskState.Rejected, closed: false),
            WakeChanges.Task(TaskState.Rejected, closed: true),
            [],
            TaskActorKind.Human,
            kind,
            TaskDecision.Decline);
        TaskChange control = WakeChanges.CloseOrReopen(TaskState.Rejected, closing: true, kind);

        AssertVerdicts([(true, true), (false, false)], Verdicts(decline), Verdicts(control));
    }

    /// <summary>A Decline taken in Needs You, as <c>DeclineCore</c> raises it (Before open in Needs You, After closed in Rejected, status field and decision), wakes both recipients.</summary>
    [Fact]
    public void Wakes_DeclineFromNeedsYouThatClosesTheTask_WakesBoth()
    {
        TaskChange decline = WakeChanges.Make(
            WakeChanges.Task(TaskState.NeedsYou, closed: false),
            WakeChanges.Task(TaskState.Rejected, closed: true),
            [new FieldChange(TaskField.Status, "Needs You", "Rejected")],
            TaskActorKind.Human,
            TaskChangeKind.Edit,
            TaskDecision.Decline);

        Assert.Equal((true, true), Verdicts(decline));
    }

    /// <summary>A decision wakes even when its only field is a silent one or a silent status move (Backlog to Cancelled).</summary>
    [Fact]
    public void Wakes_DecisionWithOnlySilentFields_StillWakes()
    {
        TaskChange rankOnly = WakeChanges.Make(WakeChanges.Task(), WakeChanges.Task(), [new FieldChange(TaskField.Rank, "2", "1")], decision: TaskDecision.Accept);
        TaskChange silentMove = WakeChanges.Make(
            WakeChanges.Task(TaskState.Backlog),
            WakeChanges.Task(TaskState.Cancelled),
            [new FieldChange(TaskField.Status, "Backlog", "Cancelled")],
            decision: TaskDecision.Decline);

        Assert.Equal((true, true), Verdicts(rankOnly));
        Assert.Equal((true, true), Verdicts(silentMove));
    }

    /// <summary>An edit of only silent fields is silent for both recipients, a rank-only move by the Human included (D-2), while a title edit by the same actor wakes.</summary>
    [Fact]
    public void Wakes_EditOfOnlyRank_IsSilentForEveryActor()
    {
        FieldChange rank = new(TaskField.Rank, "3", "1");
        FieldChange title = new(TaskField.Title, "Old", "New");

        AssertVerdicts(
            [(false, false), (false, false), (true, true)],
            Verdicts(WakeChanges.Edit(TaskActorKind.Human, rank)),
            Verdicts(WakeChanges.Edit(TaskActorKind.Agent, rank)),
            Verdicts(WakeChanges.Edit(TaskActorKind.Human, title)));
    }

    /// <summary>An edit that sets status and rank together wakes by the status, and the assignee and manager are judged separately.</summary>
    [Fact]
    public void Wakes_EditOfStatusAndRank_JudgesTheStatusPerRecipient()
    {
        TaskChange change = WakeChanges.Edit(TaskActorKind.Human, new FieldChange(TaskField.Rank, "3", "1"), new FieldChange(TaskField.Status, "To Do", "In Progress"));

        Assert.Equal((true, false), Verdicts(change));
    }
}
