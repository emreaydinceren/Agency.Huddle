using System.Globalization;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// The outcome a wake-up reports back and what it records in the Task's activity: offline assignees,
/// spent Budgets and failed sends. Split from the former single <c>TaskTriggerServiceTests</c> class;
/// the shared harness lives in <see cref="TaskTriggerTestSupport"/>.
/// </summary>
public sealed class TaskTriggerOutcomeTests
{
    /// <summary>
    /// Spec §10.4: an assignee who is a known Persona but has never registered (no user row) stops the
    /// wake - nothing is posted and no Room is created - while another Task's wake still posts. Green
    /// on arrival (9.4).
    /// </summary>
    [Fact]
    public async Task AssigneeNeverRegistered_NothingPosted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Zeta", "Zeta", "zeta", ["Platform"]), "You are Zeta.");
        Room direct = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", creator: "You", assignee: "Zeta"));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", creator: "You", assignee: "Nova", originRoomId: direct.Id));
        int roomsBefore = (await harness.Directory.GetRoomsAsync(ct)).Count;

        harness.Raise(blocked, HumanActor, "Status: To Do → In Progress");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
        Assert.Equal(roomsBefore, (await harness.Directory.GetRoomsAsync(ct)).Count);
    }

    /// <summary>Corrections-B3 D9 item 12: an Agent actor whose <see cref="TaskActor.UserId"/> is null is resolved by Name, and the wake is posted as that Agent. Green on arrival (9.4).</summary>
    [Fact]
    public async Task AgentActorWithoutUserId_ResolvedByName_PostedAsThatAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(task, new TaskActor(TaskActorKind.Agent, "Kai", null), "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>
    /// Corrections-B3 D9 item 12: an Agent actor with no user id and no user by that Name posts
    /// nothing - never falling back to the Human - while another Task's Human change still posts.
    /// Green on arrival (9.4).
    /// </summary>
    [Fact]
    public async Task AgentActorUnknown_NothingPosted_NeverAsHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", creator: "You", assignee: "Nova", originRoomId: origin.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", creator: "You", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(blocked, new TaskActor(TaskActorKind.Agent, "Phantom", null), "Status: To Do → In Progress");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
    }

    /// <summary>
    /// Spec §10.5 outcomes and §10.7: a wake posted while the assignee is online is recorded as
    /// <see cref="WakeOutcome.Woken"/> with the Task, the assignee, the Room's id and name and the
    /// fire time, and <see cref="TaskActivity.Woken"/> is raised once with that record.
    /// </summary>
    [Fact]
    public async Task Woken_RecordedInTaskActivity_WithRoomName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        List<WakeRecord> raised = [];
        harness.Activity.Woken += raised.Add;

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.Woken, record.Outcome);
        Assert.Equal("Nova", record.Assignee);
        Assert.Equal(cast.Room.Id, record.RoomId);
        Assert.Equal("Platform", record.RoomName);
        Assert.Equal(harness.Clock.GetUtcNow(), record.At);
        Assert.Equal(record, Assert.Single(raised));
    }

    /// <summary>Spec §10.5: an offline assignee is still posted to, and the outcome is <see cref="WakeOutcome.Offline"/>, with the Room recorded.</summary>
    [Fact]
    public async Task AssigneeOffline_PostedAndOutcomeOffline()
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
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.Offline, record.Outcome);
        Assert.Equal(cast.Room.Id, record.RoomId);
    }

    /// <summary>
    /// Spec §10.5 and corrections-B3 D9 item 14: the Room's Budget is spent by an Agent-authored
    /// Message after the last Human one, so Kai's wake is refused with <c>BudgetExhausted</c>. The
    /// outcome is <see cref="WakeOutcome.BudgetSpent"/> with the Room recorded, nothing escapes as an
    /// error, and the Task wake is refunded.
    /// </summary>
    [Fact]
    public async Task RoomBudgetSpent_OutcomeBudgetSpent_NoThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You", AgentMessageBudget = 1 };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        _ = await harness.Chat.PostAsync(cast.Room.Id, cast.Kai.Id, "Working on it.", ct: ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent only = Assert.Single(harness.Posts);
        Assert.Equal("Working on it.", only.Message.Text);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.BudgetSpent, record.Outcome);
        Assert.Equal(cast.Room.Id, record.RoomId);
        Assert.Equal("Platform", record.RoomName);
        Assert.Equal(0, harness.Activity.Budget(task.Id).Used);
        Assert.DoesNotContain(harness.Logger.Entries, e => e.Level >= LogLevel.Error);
    }

    /// <summary>Spec §10.6: an Agent's change to a Task whose wake budget is spent is <see cref="WakeOutcome.WakePaused"/> - recorded with no Room, and nothing posted.</summary>
    [Fact]
    public async Task WakeBudgetSpent_OutcomeWakePaused_NoRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.WakePaused, record.Outcome);
        Assert.Equal("Nova", record.Assignee);
        Assert.Null(record.RoomId);
        Assert.Null(record.RoomName);
    }

    /// <summary>Spec §10.6: ten Agent-made wakes are posted; the eleventh change is still saved, but is <see cref="WakeOutcome.WakePaused"/> and posts nothing.</summary>
    [Fact]
    public async Task TenAgentWakes_EleventhPaused_ChangeStillSaved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        for (int i = 1; i <= 11; i++)
        {
            TaskItem saved = harness.Replace(task with { Title = string.Create(CultureInfo.InvariantCulture, $"Step {i}") });
            harness.Raise(saved, cast.KaiActor, "Title changed");
            harness.Clock.Advance(DefaultWindow);
            await harness.Trigger.WhenIdleAsync();
        }

        Assert.Equal(10, harness.Posts.Count);
        Assert.Equal("Step 11", harness.Store.Get(task.Id)?.Title);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.WakePaused, record.Outcome);
    }

    /// <summary>Spec §10.6: <see cref="TaskActivity.Grant"/> on a paused Task lets the next Agent-made change wake the assignee again.</summary>
    [Fact]
    public async Task Grant_ResumesWaking()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, cast.KaiActor, "First");
        await RaiseAndFireAsync(harness, task, cast.KaiActor, "Paused");
        WakeRecord? paused = harness.Activity.LastWake(task.Id);
        harness.Activity.Grant(task.Id);
        await RaiseAndFireAsync(harness, task, cast.KaiActor, "Resumed");

        Assert.Equal(2, harness.Posts.Count);
        Assert.Equal(WakeOutcome.WakePaused, paused?.Outcome);
        Assert.Equal(WakeOutcome.Offline, harness.Activity.LastWake(task.Id)?.Outcome);
    }

    /// <summary>Spec §10.6: a Human change resets the Task's wake budget, so a paused Task's next Agent-made change wakes the assignee again.</summary>
    [Fact]
    public async Task HumanChange_ResetsBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        await RaiseAndFireAsync(harness, task, HumanActor, "By the Human");
        await RaiseAndFireAsync(harness, task, cast.KaiActor, "By Kai");

        string[] expected = ["- By the Human", "- By Kai"];
        Assert.Equal(expected, harness.Posts.Select(p => p.Message.Text));
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>Corrections-B3 D9 item 15: the budget is reset when a Human change is <b>received</b> - before any window elapses, and even when a guard (here, the Task is assigned to the Human) blocks the wake itself.</summary>
    [Fact]
    public async Task HumanChange_GuardBlocked_StillResetsBudgetOnReceipt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "You", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Raise(task, HumanActor, "Priority: Medium → High");
        WakeBudget onReceipt = harness.Activity.Budget(task.Id);
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(0, onReceipt.Used);
        Assert.False(onReceipt.Exhausted);
        Assert.Empty(harness.Posts);
    }

    /// <summary>Corrections-B3 D9 item 12 and Spec §10.5: an Agent actor with no user to post as is <see cref="WakeOutcome.Failed"/>, recorded with no Room - never posted as the Human.</summary>
    [Fact]
    public async Task UnknownAgentSender_OutcomeFailed_NoRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, new TaskActor(TaskActorKind.Agent, "Phantom", null), "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.Failed, record.Outcome);
        Assert.Equal("Nova", record.Assignee);
        Assert.Null(record.RoomId);
        Assert.Null(record.RoomName);
    }

    /// <summary>
    /// Spec §10.5: a <see cref="ChatException"/> other than <c>BudgetExhausted</c> - here from
    /// <see cref="ChatService.CreateRoomForAsync"/> meeting a stale Agent while creating step 4's Room -
    /// is <see cref="WakeOutcome.Failed"/>, recorded with no Room, and Kai's Task wake is not left counted.
    /// </summary>
    [Fact]
    public async Task StaleAgentWhileCreatingRoom_OutcomeFailed_NoRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));
        harness.Directory.HideUserId = agents.Kai.Id;

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.Failed, record.Outcome);
        Assert.Null(record.RoomId);
        Assert.Null(record.RoomName);
        Assert.Equal(0, harness.Activity.Budget(task.Id).Used);
    }
}