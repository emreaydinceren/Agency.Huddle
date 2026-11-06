using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// OR-2 (Spec §Wake): a Task change wakes the Team's manager as well as the assignee, unless the
/// manager made the change or already is the assignee, and the manager's wake counts toward the same
/// per-Task limit of Agent-made wake-ups. The shared harness lives in <see cref="TaskTriggerTestSupport"/>.
/// </summary>
public sealed class TaskTriggerManagerWakeTests
{
    /// <summary>Registers Mia as the manager Persona and user, and builds the origin Room {Human, Nova, Kai, Mia}.</summary>
    /// <param name="harness">The harness, already holding the Cast.</param>
    /// <param name="cast">Nova (assignee) and Kai (developer).</param>
    /// <param name="ct">Cancels the directory writes.</param>
    /// <returns>Mia's user and the Room.</returns>
    private static async Task<(User Mia, Room Room)> AddManagerAsync(Harness harness, Cast cast, CancellationToken ct)
    {
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Platform"]), "You are Mia.");
        User? mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct);
        Assert.NotNull(mia);
        Room room = await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, mia.Id], ct);
        return (mia, room);
    }

    /// <summary>A catalog whose Platform Team names Mia as manager.</summary>
    /// <returns>The fake catalog.</returns>
    private static FakeTeamCatalog CatalogWithManager() =>
        new() { Teams = [new TeamSummary("Platform", [], ["Nova", "Kai", "Mia"], true) { Manager = "Mia" }] };

    /// <summary>A developer changes the assignee's Task: the assignee and then the manager are each woken.</summary>
    [Fact]
    public async Task DeveloperChange_WakesAssigneeAndManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, CatalogWithManager());
        Cast cast = await StartWakingAsync(harness, ct);
        (_, Room room) = await AddManagerAsync(harness, cast, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        await RaiseAndFireAsync(harness, task, cast.KaiActor, "Status: To Do → In Progress");

        Assert.Equal(["Nova", "Mia"], harness.Posts.Select(p => p.Message.Text).ToArray());
    }

    /// <summary>
    /// #399: an Accept of a Task with an Action wakes the manager with a line that says to reassign the Action to whoever can do it
    /// and not to ask the Human, while the assignee keeps the plain go-ahead line.
    /// </summary>
    [Fact]
    public async Task AcceptWithAction_ManagerLineSaysToReassignAndNotToAskTheHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, CatalogWithManager());
        Cast cast = await StartWakingAsync(harness, ct);
        (_, Room room) = await AddManagerAsync(harness, cast, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: room.Id) with { Action = "Merge feat/x into main" });

        TaskActor human = new(TaskActorKind.Human, "You", KnownIds.Human);
        harness.Events.RaiseTaskChanged(new TaskChange(
            null,
            task,
            [],
            human,
            new ChangeLogEntry(harness.Clock.GetUtcNow(), human.Name, "accepted"),
            Agency.Huddle.App.Tasks.HumanGate.TaskDecision.Accept,
            null));
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        string[] texts = [.. harness.Posts.Select(p => p.Message.Text)];
        Assert.Equal(2, texts.Length);
        Assert.EndsWith("\nAccepted: go ahead and Merge feat/x into main, then move PLAT-0001 to Done.", texts[0], StringComparison.Ordinal); // contains-ok: the decision sentence is the Message's last line; the template above it varies.
        Assert.EndsWith(
            "\nAccepted: Merge feat/x into main. If you or the assignee can't do it, reassign PLAT-0001 to whoever can. Do not ask the Human; they have accepted it.",
            texts[1],
            StringComparison.Ordinal); // contains-ok: the decision sentence is the Message's last line; the template above it varies.
    }

    /// <summary>The manager's own change wakes only the assignee.</summary>
    [Fact]
    public async Task ManagerOwnChange_WakesOnlyAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, CatalogWithManager());
        Cast cast = await StartWakingAsync(harness, ct);
        (User mia, Room room) = await AddManagerAsync(harness, cast, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        await RaiseAndFireAsync(harness, task, new TaskActor(TaskActorKind.Agent, "Mia", mia.Id), "Status: To Do → In Progress");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Nova", post.Message.Text);
    }

    /// <summary>The manager is also the assignee: one wake, not two.</summary>
    [Fact]
    public async Task ManagerIsAssignee_WakesOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, CatalogWithManager());
        Cast cast = await StartWakingAsync(harness, ct);
        (_, Room room) = await AddManagerAsync(harness, cast, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Mia", originRoomId: room.Id));

        await RaiseAndFireAsync(harness, task, cast.KaiActor, "Status: To Do → In Progress");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Mia", post.Message.Text);
    }

    /// <summary>The manager's wake is the Task's tenth Agent-made wake: it is delivered and the budget is then spent.</summary>
    [Fact]
    public async Task ManagerWake_IsTheTenthWake_Delivered()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 10;
        using Harness harness = await CreateHarnessAsync(dir, options, ct, CatalogWithManager());
        Cast cast = await StartWakingAsync(harness, ct);
        (_, Room room) = await AddManagerAsync(harness, cast, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        for (int i = 0; i < 8; i++)
        {
            Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));
        }

        await RaiseAndFireAsync(harness, task, cast.KaiActor, "Status: To Do → In Progress");

        Assert.Equal(["Nova", "Mia"], harness.Posts.Select(p => p.Message.Text).ToArray());
        Assert.True(harness.Activity.Budget(task.Id).Exhausted);
    }

    /// <summary>The eleventh Agent-made wake is refused for the manager like any other, and is not counted.</summary>
    [Fact]
    public async Task ManagerWake_IsTheEleventhWake_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 10;
        using Harness harness = await CreateHarnessAsync(dir, options, ct, CatalogWithManager());
        Cast cast = await StartWakingAsync(harness, ct);
        (_, Room room) = await AddManagerAsync(harness, cast, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        for (int i = 0; i < 9; i++)
        {
            Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));
        }

        await RaiseAndFireAsync(harness, task, cast.KaiActor, "Status: To Do → In Progress");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Nova", post.Message.Text);
        Assert.Equal(10, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>A Team the catalog does not know (no team file) wakes only the assignee, as before.</summary>
    [Fact]
    public async Task TeamWithoutTeamFile_WakesOnlyAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, new FakeTeamCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, cast.KaiActor, "Status: To Do → In Progress");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Nova", post.Message.Text);
    }
}
