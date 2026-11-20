using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.MentionTestSupport;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// SW-T4 (smart wakes spec 3.2, 3.3, 5.3 and corrections 8 to 11): a comment wakes only the Agents it Mentions. The assignee is Nova, the Platform
/// Team's manager Rex (so a manager who is not Mentioned shows as not woken), the usual actor Kai, and Rex's Alias is <c>rexy</c>. Every Message is
/// the probe template <c>{{assignee}}|{{actor}}|{{changes}}</c>, so each text starts with the Name that was woken.
/// </summary>
public sealed class TaskTriggerServiceMentionTests
{
    /// <summary>A comment with no Mention wakes no one, the assignee and the manager included: it is dropped on receipt and the wake budget is untouched.</summary>
    [Fact]
    public async Task Comment_WithoutAMention_WakesNoOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, RexManagerCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Looks fine to me."));
        await FireAsync(harness);

        Assert.Empty(harness.Posts);
        Assert.Null(harness.Activity.LastWake(task.Id));
        Assert.Equal(0, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>A comment Mentioning Rex wakes Rex only, once, with the comment's text in the bullet, Mentions neutralised; the assignee Nova is not woken.</summary>
    [Fact]
    public async Task Comment_MentioningRex_WakesOnlyRexWithTheCommentText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Ready for another look, @Rex."));
        await FireAsync(harness);

        Assert.Equal(["Rex|Kai|" + Bullet("Kai", "Ready for another look, @Rex.")], Texts(harness));
    }

    /// <summary>A comment Mentioning the assignee Nova wakes Nova only, not the Team's manager Rex: the Mention rule wakes by name and nothing else rides along.</summary>
    [Fact]
    public async Task Comment_MentioningTheAssignee_WakesOnlyTheAssigneeNotTheManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, RexManagerCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Over to you, @Nova."));
        await FireAsync(harness);

        Assert.Equal(["Nova|Kai|" + Bullet("Kai", "Over to you, @Nova.")], Texts(harness));
    }

    /// <summary>A Persona's Alias resolves like its Name: <c>@rexy</c> wakes Rex, and the Message names Rex.</summary>
    [Fact]
    public async Task Comment_MentioningAnAlias_WakesThePersona()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Thoughts, @rexy?"));
        await FireAsync(harness);

        Assert.Equal(["Rex|Kai|" + Bullet("Kai", "Thoughts, @rexy?")], Texts(harness));
    }

    /// <summary>The author Mentioning themselves, the Human, a name nobody has, or a user that is no registered Persona wakes no one.</summary>
    /// <param name="text">The comment text.</param>
    [Theory]
    [InlineData("A note to self, @Kai.")]
    [InlineData("Your call, @You.")]
    [InlineData("Is @Zed around?")]
    [InlineData("Is @Ghost around?")]
    public async Task Comment_MentioningNoWakeableAgent_WakesNoOne(string text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, RexManagerCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        await AddGhostUserAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, text));
        await FireAsync(harness);

        Assert.Empty(harness.Posts);
        Assert.Null(harness.Activity.LastWake(task.Id));
    }

    /// <summary><c>@all</c> in a Task comment wakes no one, though it names every Agent in a chat Room (correction 9).</summary>
    [Fact]
    public async Task Comment_MentioningAll_WakesNoOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, RexManagerCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Everyone, @all please read this."));
        await FireAsync(harness);

        Assert.Empty(harness.Posts);
    }

    /// <summary><c>@all</c> beside a named Agent wakes only the named Agent: the <c>@all</c> expansion does not leak into the Mentioned set.</summary>
    [Fact]
    public async Task Comment_MentioningAllAndRex_WakesOnlyRex()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "@all FYI, and @Rex please act."));
        await FireAsync(harness);

        Assert.Equal(["Rex|Kai|" + Bullet("Kai", "@all FYI, and @Rex please act.")], Texts(harness));
    }

    /// <summary>With <c>Tasks.WakeEnabled</c> turned off (the options are read live) a Mentioning comment wakes no one, though the same comment woke Rex a moment before.</summary>
    [Fact]
    public async Task Comment_MentioningRex_WakeUpsOff_WakesNoOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Ready, @Rex."));
        await FireAsync(harness);
        Assert.Equal(["Rex|Kai|" + Bullet("Kai", "Ready, @Rex.")], Texts(harness));
        options.Tasks.WakeEnabled = false;

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Ready again, @Rex."));
        await FireAsync(harness);

        Assert.Single(harness.Posts);
    }

    /// <summary>Rex is the assignee of a status change and Mentioned in a comment of the same batch: one Message, listing both bullets in arrival order.</summary>
    [Fact]
    public async Task StatusChangeAndMention_OfTheSameAgent_PostsOneMessageWithBothBullets()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Rex", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Move(harness, task, cast.KaiActor, TaskState.ToDo, TaskState.InProgress));
        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Start with the parser, @Rex."));
        await FireAsync(harness);

        Assert.Equal(
            ["Rex|Kai|- " + MoveSummary(TaskState.ToDo, TaskState.InProgress) + "\n" + Bullet("Kai", "Start with the parser, @Rex.")],
            Texts(harness));
    }

    /// <summary>The Mentioned set is the union over the batch's comments in first-mention order, case-insensitive: each Agent gets one Message listing every bullet.</summary>
    [Fact]
    public async Task TwoComments_InOneBatch_WakeTheUnionInFirstMentionOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "First, @Rex."));
        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Second, @nova and @REX."));
        await FireAsync(harness);

        string bullets = Bullet("Kai", "First, @Rex.") + "\n" + Bullet("Kai", "Second, @nova and @REX.");
        Assert.Equal(["Rex|Kai|" + bullets, "Nova|Kai|" + bullets], Texts(harness));
    }

    /// <summary>A comment longer than 1,000 characters is cut at 1,000, the marker follows inside the quote, and the Mention past the cut still wakes (resolution reads the whole text).</summary>
    [Fact]
    public async Task LongComment_IsCutAtOneThousandCharactersWithAMarker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        string text = new string('x', 1000) + " @Rex";

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, text));
        await FireAsync(harness);

        Assert.Equal(["Rex|Kai|" + Bullet("Kai", new string('x', 1000) + CutMarker)], Texts(harness));
    }

    /// <summary>A comment of exactly 1,000 characters is shown whole, with its Mention neutralised and no marker; the limit is on the text as written.</summary>
    [Fact]
    public async Task CommentOfExactlyOneThousandCharacters_IsNotCut()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        string text = new string('x', 995) + " @Rex";

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, text));
        await FireAsync(harness);

        Assert.Equal(1000, text.Length);
        Assert.Equal(["Rex|Kai|" + Bullet("Kai", text)], Texts(harness));
    }

    /// <summary>
    /// An Agent author's Mentions spend the Task's wake budget: with a budget of one, the first Mention posts and the second is held (outcome WakePaused,
    /// nothing posted); allowing more replays the held one, with its comment text, as one Message.
    /// </summary>
    [Fact]
    public async Task AgentAuthor_MentionSpendsTheWakeBudget_AndIsHeldWhenItIsSpent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "First, @Rex."));
        await FireAsync(harness);
        Assert.Equal(["Rex|Kai|" + Bullet("Kai", "First, @Rex.")], Texts(harness));
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Second, @Rex."));
        await FireAsync(harness);

        Assert.Single(harness.Posts);
        Assert.True(harness.Trigger.IsHeld(task.Id));
        Assert.Equal(WakeOutcome.WakePaused, harness.Activity.LastWake(task.Id)?.Outcome);

        harness.Activity.Grant(task.Id);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(["Rex|Kai|" + Bullet("Kai", "First, @Rex."), "Rex|Kai|" + Bullet("Kai", "Second, @Rex.")], Texts(harness));
        Assert.False(harness.Trigger.IsHeld(task.Id));
    }

    /// <summary>
    /// Hold merges a held Mention with a later refused change for the same Agent, earlier entries first and each comment text aligned with its entry: the
    /// replay is one Message, the comment bullet first and then the status bullet.
    /// </summary>
    [Fact]
    public async Task HeldMention_MergedWithALaterHeldChange_KeepsTheCommentTextAlignedWithItsEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Rex", originRoomId: room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Look at this first, @Rex."));
        await FireAsync(harness);
        harness.Events.RaiseTaskChanged(Move(harness, task, cast.KaiActor, TaskState.ToDo, TaskState.InProgress));
        await FireAsync(harness);
        Assert.Empty(harness.Posts);
        Assert.True(harness.Trigger.IsHeld(task.Id));

        harness.Activity.Grant(task.Id);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(
            ["Rex|Kai|" + Bullet("Kai", "Look at this first, @Rex.") + "\n- " + MoveSummary(TaskState.ToDo, TaskState.InProgress)],
            Texts(harness));
    }

    /// <summary>Rex is held for the assignee wake and Mentioned in the same batch: the round skips the Mention because the name is already held, so one held wake is replayed, not two.</summary>
    [Fact]
    public async Task StatusChangeAndMention_OfTheSameAgentWithTheBudgetSpent_HoldsOneWake()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Rex", originRoomId: room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Events.RaiseTaskChanged(Move(harness, task, cast.KaiActor, TaskState.ToDo, TaskState.InProgress));
        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Start with the parser, @Rex."));
        await FireAsync(harness);
        Assert.Empty(harness.Posts);

        harness.Activity.Grant(task.Id);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(
            ["Rex|Kai|- " + MoveSummary(TaskState.ToDo, TaskState.InProgress) + "\n" + Bullet("Kai", "Start with the parser, @Rex.")],
            Texts(harness));
    }

    /// <summary>A kept Human comment that Mentions an Agent resets the wake budget and forgets a held wake (correction 2), and wakes the Mentioned Agent as the Human without spending the budget.</summary>
    [Fact]
    public async Task HumanComment_WithAMention_ResetsTheBudgetForgetsTheHeldWakeAndWakesRex()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));
        harness.Events.RaiseTaskChanged(Move(harness, task, cast.KaiActor, TaskState.ToDo, TaskState.InProgress));
        await FireAsync(harness);
        Assert.True(harness.Trigger.IsHeld(task.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, HumanActor, "Rex, please take a look: @Rex."));
        await FireAsync(harness);

        Assert.False(harness.Trigger.IsHeld(task.Id));
        Assert.False(harness.Activity.Budget(task.Id).Exhausted);
        Assert.Equal(["Rex|You|" + Bullet("You", "Rex, please take a look: @Rex.")], Texts(harness));
    }

    /// <summary>A Human comment whose text holds an <c>@</c> no Agent answers to is still kept on receipt, so it resets the budget and forgets a held wake, and wakes no one.</summary>
    [Fact]
    public async Task HumanComment_WithAnAtNobodyAnswersTo_StillResetsTheBudgetAndForgetsTheHeldWake()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));
        harness.Events.RaiseTaskChanged(Move(harness, task, cast.KaiActor, TaskState.ToDo, TaskState.InProgress));
        await FireAsync(harness);
        Assert.True(harness.Trigger.IsHeld(task.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, HumanActor, "Mail me at you@example.com, @Zed."));
        await FireAsync(harness);

        Assert.False(harness.Trigger.IsHeld(task.Id));
        Assert.False(harness.Activity.Budget(task.Id).Exhausted);
        Assert.Empty(harness.Posts);
    }

    /// <summary>A Human comment with no <c>@</c> is dropped like any silent change: it resets nothing and forgets nothing, so a held wake stays held and the budget stays spent.</summary>
    [Fact]
    public async Task HumanComment_WithoutAnAt_LeavesTheHeldWakeHeldAndTheBudgetSpent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));
        harness.Events.RaiseTaskChanged(Move(harness, task, cast.KaiActor, TaskState.ToDo, TaskState.InProgress));
        await FireAsync(harness);
        Assert.True(harness.Trigger.IsHeld(task.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, HumanActor, "Thanks, nothing more from me."));
        await FireAsync(harness);

        Assert.True(harness.Trigger.IsHeld(task.Id));
        Assert.True(harness.Activity.Budget(task.Id).Exhausted);
        Assert.Empty(harness.Posts);
    }

    /// <summary>An Agent's comment with an <c>@</c> but no resolvable Agent wakes no one and holds nothing, even with the wake budget spent: there is no wake to hold.</summary>
    [Fact]
    public async Task Comment_WithAnAtButNoResolvableAgent_WakesNoOneAndHoldsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Events.RaiseTaskChanged(Comment(harness, task, cast.KaiActor, "Write to me@example.com or ask @Zed."));
        await FireAsync(harness);

        Assert.False(harness.Trigger.IsHeld(task.Id));
        Assert.Empty(harness.Posts);
        Assert.Null(harness.Activity.LastWake(task.Id));
    }
}
