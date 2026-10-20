using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// The wake <see cref="TaskTriggerService"/> posts when a Helper changes a Task (Helpers Spec §6.12, plan HL-14): the
/// assignee hears about it, the Human is the sender, the Helper is named, and a Teammate's wake budget is not spent.
/// </summary>
public sealed class TaskTriggerHelperTests
{
    private const string GateOnSummary = "human gate on (reason: Sends a reply to a customer)";

    /// <summary>A Helper turning the gate on wakes the assignee, posted as the Human, naming the Helper and listing the change.</summary>
    [Fact]
    public async Task HelperGateOn_WakesTheAssigneeAsTheHumanNamingTheHelper()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, TaskActors.Helper("Needs Human"), GateOnSummary);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
        Assert.Contains("was changed by Needs Human (Helper):", post.Message.Text, StringComparison.Ordinal);
        Assert.Contains(GateOnSummary, post.Message.Text, StringComparison.Ordinal);
        Assert.StartsWith("@Nova ", post.Message.Text, StringComparison.Ordinal);
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
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        harness.Raise(task, TaskActors.Helper("Needs Human"), GateOnSummary);
        harness.Raise(task, HumanActor, "priority: Medium → High");

        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(2, harness.Posts.Count);
        Assert.Contains(harness.Posts, static post => post.Message.Text.Contains("changed by Needs Human (Helper):", StringComparison.Ordinal));
        Assert.Contains(harness.Posts, static post => post.Message.Text.Contains("changed by You:", StringComparison.Ordinal));
    }
}
