using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// The wake <see cref="TaskTriggerService"/> posts when a Helper changes a Task (Helpers Spec §6.12, plan HL-14, and Helpers in
/// the Feed spec §5.6): the assignee hears about it, Huddle is the sender (ADR-0065), the Helper is named, and a Teammate's
/// wake budget is not spent.
/// </summary>
public sealed class TaskTriggerHelperTests
{
    private const string GateOnSummary = "human gate on (reason: Sends a reply to a customer)";

    /// <summary>A Helper turning the gate on wakes the assignee, posted by Huddle and not the Human, naming the Helper and listing the change.</summary>
    [Fact]
    public async Task HelperGateOn_WakesTheAssigneeAsHuddleNamingTheHelper()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        harness.Prompts.SetOverride(WakeMessageKey, "@{{assignee}} {{actor}} | {{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, TaskActors.Helper("Needs Human"), GateOnSummary);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal((KnownIds.Huddle, "Huddle"), (post.Message.SenderId, post.Message.SenderName));
        Assert.Equal("@Nova Needs Human (Helper) | - human gate on (reason: Sends a reply to a customer)", post.Message.Text);
        Assert.Equal(WakeOutcome.Woken, harness.Activity.LastWake(task.Id)?.Outcome);
        Assert.Equal(cast.Nova.Id, Assert.Single(post.Mentions).Id);
    }

    /// <summary>
    /// With no origin Room the Helper's wake goes to the Room {Human, assignee}: Huddle takes no seat, so it is in no member set and
    /// not among the Members of the Room that is found or created for it.
    /// </summary>
    [Fact]
    public async Task HelperWake_WithoutAnOriginRoom_UsesTheAssigneesRoomAndSeatsNoHuddle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", creator: "You"));

        await RaiseAndFireAsync(harness, task, TaskActors.Helper("Needs Human"), GateOnSummary);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(KnownIds.Huddle, post.Message.SenderId);
        Assert.NotEqual(cast.Room.Id, post.Room.Id);
        Assert.Equal(new HashSet<string> { KnownIds.Human, cast.Nova.Id }, post.Members.Select(static member => member.Id).ToHashSet(StringComparer.Ordinal));
        Assert.Equal(WakeOutcome.Woken, harness.Activity.LastWake(task.Id)?.Outcome);
    }

    /// <summary>The origin Room is used when the assignee is a Member, though Huddle is not and cannot be.</summary>
    [Fact]
    public async Task HelperWake_UsesTheOriginRoomThoughHuddleIsNoMemberOfIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, TaskActors.Helper("Needs Human"), GateOnSummary);

        Assert.Equal(cast.Room.Id, Assert.Single(harness.Posts).Room.Id);
    }

    /// <summary>A Teammate's change is still posted by that Teammate and the Human's by the Human: only a Helper's wake moved to Huddle.</summary>
    [Fact]
    public async Task OtherActors_StillPostAsThemselves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, HumanActor, "priority: Medium → High");
        await RaiseAndFireAsync(harness, task, cast.KaiActor, "priority: High → Low");

        Assert.Equal([KnownIds.Human, cast.Kai.Id], harness.Posts.Select(static post => post.Message.SenderId));
    }

    /// <summary>A Helper's change does not spend the Task's Teammate wake budget, however many times it wakes.</summary>
    [Fact]
    public async Task HelperChange_ConsumesNoWakeBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, TaskActors.Helper("Needs Human"), GateOnSummary);

        Assert.Single(harness.Posts);
        Assert.Equal(0, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>The Helper's batch is its own: a Helper change and a Teammate change in one window are two wakes, each naming its actor.</summary>
    [Fact]
    public async Task HelperAndTeammateChanges_AreSeparateBatches()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{actor}}");
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        harness.Raise(task, TaskActors.Helper("Needs Human"), GateOnSummary);
        harness.Raise(task, HumanActor, "priority: Medium → High");

        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(["Needs Human (Helper)", "You"], harness.Posts.Select(static post => post.Message.Text).Order(StringComparer.Ordinal));
    }

    /// <summary>A Helper's wake into a Room whose Budget is spent is not posted and is recorded as BudgetSpent, as an Agent's is, instead of reading Woken while the assignee's reply is refused.</summary>
    [Fact]
    public async Task HelperWake_IntoASpentRoom_IsRecordedAsBudgetSpentAndNotPosted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You", AgentMessageBudget = 1 }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        _ = await harness.Chat.PostAsync(cast.Room.Id, cast.Kai.Id, "Working on it.", ct: ct);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, TaskActors.Helper("Needs Human"), GateOnSummary);

        Assert.Equal("Working on it.", Assert.Single(harness.Posts).Message.Text);
        Assert.Equal(WakeOutcome.BudgetSpent, harness.Activity.LastWake(task.Id)?.Outcome);
    }
}
