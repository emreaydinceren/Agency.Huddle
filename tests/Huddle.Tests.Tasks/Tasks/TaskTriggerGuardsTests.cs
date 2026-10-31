using Microsoft.Extensions.Logging;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// The guards, Budgets and lifecycle of <see cref="TaskTriggerService"/>'s wake-up fires: what blocks
/// a wake, what a wake costs, and how starting, stopping and disposing behave. Split from the former
/// single <c>TaskTriggerServiceTests</c> class; the shared harness lives in
/// <see cref="TaskTriggerTestSupport"/>.
/// </summary>
public sealed class TaskTriggerGuardsTests
{
    /// <summary>Spec §10.2 guard 4, applied when the batch fires: the assignee's own change posts nothing, while another Task's change in the same window does.</summary>
    [Fact]
    public async Task GuardBlocked_SelfEdit_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(blocked, cast.NovaActor, "Nova edited her own Task");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
    }

    /// <summary>Spec §10.2 guard 3 on the <b>latest</b> Task (Spec §10.3): reassigned to the Human inside the window, the batch posts nothing.</summary>
    [Fact]
    public async Task GuardBlocked_ReassignedToHumanDuringWindow_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(blocked, HumanActor, "Priority: Medium → High");
        TaskItem mine = harness.Replace(blocked with { Assignee = "You" });
        harness.Raise(mine, HumanActor, "Assignee: Nova → You");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
    }

    /// <summary>Spec §10.2 guard 5, applied when the batch fires: an assignee that isn't a known Persona posts nothing.</summary>
    [Fact]
    public async Task GuardBlocked_UnknownPersona_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Ghost", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(blocked, HumanActor, "Status: To Do → In Progress");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
    }

    /// <summary>Spec §10.2 guard 6 through <c>TryConsumeAgentWake</c> (corrections-B3 D9 item 9): an Agent's change to a Task whose wake budget is spent posts nothing, and consumes nothing more.</summary>
    [Fact]
    public async Task GuardBlocked_AgentWakeBudgetSpent_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(blocked.Id));

        harness.Raise(blocked, cast.KaiActor, "Status: To Do → In Progress");
        harness.Raise(control, cast.KaiActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
        Assert.Equal(1, harness.Activity.Budget(blocked.Id).Used);
    }

    /// <summary>Corrections-B3 D9 item 9: an Agent-made wake whose outcome is <see cref="WakeOutcome.Offline"/> (the assignee isn't connected) is counted against the Task's wake budget.</summary>
    [Fact]
    public async Task AgentChange_AssigneeOffline_CountsOneWake()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>Corrections-B3 D9 items 9 and 11: an Agent-made wake whose outcome is <see cref="WakeOutcome.Woken"/> (the assignee is online) is counted against the Task's wake budget.</summary>
    [Fact]
    public async Task AgentChange_AssigneeOnline_CountsOneWake()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>
    /// Corrections-B3 D9 item 9: a wake that doesn't end <see cref="WakeOutcome.Woken"/> or
    /// <see cref="WakeOutcome.Offline"/> is refunded. Kai's first wake fills the Room's one-message
    /// Budget; the second is refused by <see cref="ChatService"/>, so its consumed wake is given back.
    /// </summary>
    [Fact]
    public async Task AgentChange_RoomBudgetSpent_RefundsWake()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You", AgentMessageBudget = 1 };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "First");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
        harness.Raise(task, cast.KaiActor, "Second");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>Spec §10.6: only Agent-made wakes count - a Human-made wake posts without touching the Task's wake budget.</summary>
    [Fact]
    public async Task HumanChange_DoesNotConsumeWakeBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        Assert.Equal(0, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>Corrections-B3 D9 item 18: an <see cref="TaskActorKind.OutsideHuddle"/> change is still woken (as the Human) but does not reset the Task's wake budget the way a Human change does.</summary>
    [Fact]
    public async Task OutsideHuddleChange_DoesNotResetWakeBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Raise(task, OutsideActor, "Edited outside Huddle");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
        Assert.True(harness.Activity.Budget(task.Id).Exhausted);
    }

    /// <summary>Corrections-B3 D9 item 18: more than 10 outside-edit changes delivered within one coalesce window (a <c>git pull</c>) are logged, and nobody is woken.</summary>
    [Fact]
    public async Task OutsideEdits_MoreThanTenInOneWindow_LoggedNotWoken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem[] tasks = SeedNumbered(harness, cast, count: 11);

        foreach (TaskItem task in tasks)
        {
            harness.Raise(task, OutsideActor, "Pulled from git");
        }

        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
        Assert.Contains(harness.Logger.Entries, e => e.Level >= LogLevel.Information);
    }

    /// <summary>Corrections-B3 D9 item 18, the boundary: exactly 10 outside-edit changes in one window are all woken.</summary>
    [Fact]
    public async Task OutsideEdits_TenInOneWindow_AllWoken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem[] tasks = SeedNumbered(harness, cast, count: 10);

        foreach (TaskItem task in tasks)
        {
            harness.Raise(task, OutsideActor, "Pulled from git");
        }

        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(10, harness.Posts.Count);
    }

    /// <summary>Corrections-B3 D9 item 18 applies to outside edits only: eleven Human changes to eleven Tasks in one window all wake.</summary>
    [Fact]
    public async Task HumanEdits_ElevenInOneWindow_AllWoken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem[] tasks = SeedNumbered(harness, cast, count: 11);

        foreach (TaskItem task in tasks)
        {
            harness.Raise(task, HumanActor, "Bulk edit");
        }

        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(11, harness.Posts.Count);
    }

    /// <summary>Corrections-B3 D9 item 7: the batch is removed when its fire <b>starts</b>, so a change arriving while that fire is still posting opens a fresh batch - it is neither folded into the in-flight Message nor lost.</summary>
    [Fact]
    public async Task ChangeDuringFire_StartsNewBatch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int held = 0;
        harness.Directory.BeforeGetRoomMembers = () =>
        {
            if (Interlocked.Exchange(ref held, 1) != 0)
            {
                return Task.CompletedTask;
            }

            entered.TrySetResult();
            return release.Task;
        };

        harness.Raise(task, HumanActor, "First");
        harness.Clock.Advance(DefaultWindow);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        harness.Raise(task, HumanActor, "Second");
        release.TrySetResult();
        await harness.Trigger.WhenIdleAsync();
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        string[] expected = ["- First", "- Second"];
        Assert.Equal(expected, harness.Posts.Select(p => p.Message.Text));
    }

    /// <summary>Corrections-B3 D9 item 7: <see cref="TaskTriggerService.StopAsync"/> disposes a pending batch's timer, so a batch still inside its window posts nothing even when the window later elapses.</summary>
    [Fact]
    public async Task StopAsync_PendingBatch_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        harness.Raise(task, HumanActor, "Before stop");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        harness.Raise(task, HumanActor, "Pending at stop");
        await harness.Trigger.StopAsync(ct);
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- Before stop", post.Message.Text);
    }

    /// <summary>Corrections-B3 D9 items 7-8: <see cref="TaskTriggerService.StopAsync"/> awaits a fire that is already in flight - it does not complete while the fire is held open, and does once it is released.</summary>
    [Fact]
    public async Task StopAsync_InFlightFire_AwaitsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 0;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Directory.BeforeGetRoomMembers = () =>
        {
            entered.TrySetResult();
            return release.Task;
        };

        harness.Raise(task, HumanActor, "In flight at stop");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Task stop = harness.Trigger.StopAsync(ct);
        bool completedWhileHeld = stop.IsCompleted;
        release.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.False(completedWhileHeld);
        Assert.True(stop.IsCompletedSuccessfully);
    }

    /// <summary>Corrections-B3 D9 item 7 and Spec §10.1: the service subscribes to <see cref="TaskEvents.TaskChanged"/> in <see cref="TaskTriggerService.StartAsync"/>, not in its constructor, and <see cref="TaskTriggerService.StopAsync"/> unsubscribes.</summary>
    [Fact]
    public async Task StartAsync_Subscribes_StopAsyncUnsubscribes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);

        int beforeStart = SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged));
        await harness.Trigger.StartAsync(ct);
        int afterStart = SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged));
        await harness.Trigger.StopAsync(ct);
        int afterStop = SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged));

        Assert.Equal(0, beforeStart);
        Assert.Equal(1, afterStart);
        Assert.Equal(0, afterStop);
    }

    /// <summary>Spec §10.1: <see cref="TaskTriggerService.Dispose"/> unsubscribes from <see cref="TaskEvents.TaskChanged"/> too.</summary>
    [Fact]
    public async Task Dispose_Unsubscribes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        await harness.Trigger.StartAsync(ct);
        int afterStart = SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged));

        harness.Trigger.Dispose();

        Assert.Equal(1, afterStart);
        Assert.Equal(0, SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged)));
    }

    /// <summary>Corrections-B3 D9 item 7: a change raised before <see cref="TaskTriggerService.StartAsync"/> is not seen at all; one raised after it is.</summary>
    [Fact]
    public async Task ChangeBeforeStartAsync_Ignored_ChangeAfterIsWoken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 0;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct, start: false);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Before start");
        await harness.Trigger.WhenIdleAsync();
        await harness.Trigger.StartAsync(ct);
        harness.Raise(task, HumanActor, "After start");
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- After start", post.Message.Text);
    }

    /// <summary>Corrections-B3 D9 item 7: <c>FireAsync</c> catches and logs whatever a fire throws - <see cref="TaskTriggerService.WhenIdleAsync"/> does not rethrow it, and the next wake still posts.</summary>
    [Fact]
    public async Task FireThrows_ErrorLogged_LaterWakeStillPosts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 0;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        int thrown = 0;
        harness.Directory.BeforeGetRoomMembers = () => Interlocked.Exchange(ref thrown, 1) == 0
            ? Task.FromException(new InvalidOperationException("injected directory failure"))
            : Task.CompletedTask;

        harness.Raise(task, HumanActor, "Fails");
        await harness.Trigger.WhenIdleAsync();
        harness.Raise(task, HumanActor, "Succeeds");
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- Succeeds", post.Message.Text);
        Assert.Contains(harness.Logger.Entries, e => e.Exception is InvalidOperationException);
    }
}