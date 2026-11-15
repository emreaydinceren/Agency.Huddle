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
/// RQ-6 (Spec §Wake): when a Task reaches Done, the assignees of Tasks it blocks are woken once every
/// one of their blockers is Done; a blocked Task with no assignee wakes its Team's manager instead.
/// The shared harness lives in <see cref="TaskTriggerTestSupport"/>.
/// </summary>
public sealed class TaskTriggerBlockedTests
{
    /// <summary>The change-log entry a Done transition leaves on the Task that was finished.</summary>
    private static readonly ChangeLogEntry DoneEntry = TestTasks.Entry("2026-10-01T09:00:00Z", "Kai", "status: In Progress → Done");

    /// <summary>A blocked Task that was Cancelled is closed, so its last blocker finishing wakes nobody (review fix item 4).</summary>
    [Fact]
    public async Task BlockedTaskCancelled_WakesNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem blocker = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", status: TaskState.Cancelled, assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [blocker.Id]));

        await RaiseAndFireAsync(harness, blocker, cast.KaiActor, "status: In Progress → Done");

        Assert.Empty(harness.Posts);
    }

    /// <summary>Two blockers finishing together wake the blocked Task's assignee once, not twice.</summary>
    [Fact]
    public async Task TwoBlockersFinishTogether_WakesOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem first = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        TaskItem second = SeedBlocker(harness, "OPS-0002", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [first.Id, second.Id]));

        await RaiseAndFireAsync(harness, first, cast.KaiActor, "status: In Progress → Done");
        await RaiseAndFireAsync(harness, second, cast.KaiActor, "status: In Progress → Done");

        Assert.Single(harness.Posts);
    }

    /// <summary>A wake that is never delivered (the manager has no user row) leaves no dedupe marker, so a later Done event can still wake.</summary>
    [Fact]
    public async Task UndeliveredWake_LeavesNoMarker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Platform", [], ["Nova", "Kai", "Mia"], true) { Manager = "Mia" }] };
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog);
        Cast cast = await StartWakingAsync(harness, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Platform"]), "You are Mia.");
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem blocker = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", originRoomId: cast.Room.Id, blockedBy: [blocker.Id]));

        await RaiseAndFireAsync(harness, blocker, cast.KaiActor, "status: In Progress → Done");

        Assert.Empty(harness.Posts);
        Assert.DoesNotContain(harness.Store.All.Single(task => task.Id.ToString() == "PLAT-0007").ChangeLog, entry => entry.Summary.StartsWith("Unblocked:", StringComparison.Ordinal));
    }

    /// <summary>Seeds a Task in the Ops Team, which has no manager, so finishing it wakes nobody by itself.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="id">The Task id.</param>
    /// <param name="status">The Task's status.</param>
    /// <returns>The Task as stored.</returns>
    private static TaskItem SeedBlocker(Harness harness, string id, TaskState status) =>
        harness.Seed(TestTasks.Make(
            id: id,
            status: status,
            location: new TaskLocation("Ops", null, false),
            changeLog: status == TaskState.Done ? [DoneEntry] : []));

    /// <summary>Registers Mia as the manager Persona and user and builds the origin Room {Human, Nova, Kai, Mia}.</summary>
    /// <param name="harness">The harness, already holding the Cast.</param>
    /// <param name="cast">Nova and Kai.</param>
    /// <param name="ct">Cancels the directory writes.</param>
    /// <returns>The Room.</returns>
    private static async Task<Room> AddManagerAsync(Harness harness, Cast cast, CancellationToken ct)
    {
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Platform"]), "You are Mia.");
        User? mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct);
        Assert.NotNull(mia);
        return await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, mia.Id], ct);
    }

    /// <summary>One of two blockers is Done: the blocked Task's assignee is not woken yet.</summary>
    [Fact]
    public async Task OneOfTwoBlockersDone_WakesNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem first = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        TaskItem second = SeedBlocker(harness, "OPS-0002", TaskState.InProgress);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [first.Id, second.Id]));

        await RaiseAndFireAsync(harness, first, cast.KaiActor, "status: In Progress → Done");

        Assert.Empty(harness.Posts);
    }

    /// <summary>The last blocker reaches Done: the blocked Task's assignee is woken once.</summary>
    [Fact]
    public async Task LastBlockerDone_WakesAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem earlier = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        TaskItem last = SeedBlocker(harness, "OPS-0002", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [earlier.Id, last.Id]));

        await RaiseAndFireAsync(harness, last, cast.KaiActor, "status: In Progress → Done");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Nova", post.Message.Text);
    }

    /// <summary>A blocked Task with no assignee wakes its Team's manager.</summary>
    [Fact]
    public async Task NoAssignee_WakesTeamManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Platform", [], ["Nova", "Kai", "Mia"], true) { Manager = "Mia" }] };
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddManagerAsync(harness, cast, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem blocker = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", originRoomId: room.Id, blockedBy: [blocker.Id]));

        await RaiseAndFireAsync(harness, blocker, cast.KaiActor, "status: In Progress → Done");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Mia", post.Message.Text);
    }

    /// <summary>The blocker lives in another Team than the blocked Task: the assignee is still woken.</summary>
    [Fact]
    public async Task BlockerInOtherTeam_WakesAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}|{{taskId}}|{{team}}");
        TaskItem blocker = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [blocker.Id]));

        await RaiseAndFireAsync(harness, blocker, cast.KaiActor, "status: In Progress → Done");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Nova|PLAT-0007|Platform", post.Message.Text);
    }

    /// <summary>The same Done event twice wakes the assignee once.</summary>
    [Fact]
    public async Task DuplicateDoneEvent_WakesOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem blocker = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [blocker.Id]));

        await RaiseAndFireAsync(harness, blocker, cast.KaiActor, "status: In Progress → Done");
        await RaiseAndFireAsync(harness, blocker, cast.KaiActor, "status: In Progress → Done");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Nova", post.Message.Text);
    }

    /// <summary>A blocker reopened and finished again wakes the assignee a second time.</summary>
    [Fact]
    public async Task ReopenedAndDoneAgain_WakesAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem blocker = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [blocker.Id]));
        await RaiseAndFireAsync(harness, blocker, cast.KaiActor, "status: In Progress → Done");
        ChangeLogEntry reopened = TestTasks.Entry("2026-10-02T09:00:00Z", "Kai", "status: Done → In Progress");
        ChangeLogEntry doneAgain = TestTasks.Entry("2026-10-03T09:00:00Z", "Kai", "status: In Progress → Done");
        TaskItem again = harness.Replace(blocker with { ChangeLog = [DoneEntry, reopened, doneAgain] });

        await RaiseAndFireAsync(harness, again, cast.KaiActor, "status: In Progress → Done");

        Assert.Equal(["Nova", "Nova"], harness.Posts.Select(p => p.Message.Text).ToArray());
    }

    /// <summary>A blocked Task that is itself already Done wakes nobody.</summary>
    [Fact]
    public async Task BlockedTaskAlreadyDone_WakesNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem blocker = SeedBlocker(harness, "OPS-0001", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", status: TaskState.Done, assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [blocker.Id]));

        await RaiseAndFireAsync(harness, blocker, cast.KaiActor, "status: In Progress → Done");

        Assert.Empty(harness.Posts);
    }

    /// <summary>The last blocker is Cancelled, which finishes it like Done (#538): the blocked Task's assignee is woken once.</summary>
    [Fact]
    public async Task LastBlockerCancelled_WakesAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem blocker = SeedBlocker(harness, "OPS-0001", TaskState.Cancelled);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [blocker.Id]));

        await RaiseAndFireAsync(harness, blocker, cast.KaiActor, "status: In Progress → Cancelled");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Nova", post.Message.Text);
    }

    /// <summary>One blocker was Rejected earlier and the other reaches Done: every blocker is terminal, so the assignee is woken.</summary>
    [Fact]
    public async Task EarlierBlockerRejected_LastDone_WakesAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem rejected = SeedBlocker(harness, "OPS-0001", TaskState.Rejected);
        TaskItem last = SeedBlocker(harness, "OPS-0002", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [rejected.Id, last.Id]));

        await RaiseAndFireAsync(harness, last, cast.KaiActor, "status: In Progress → Done");

        Assert.Single(harness.Posts);
    }

    /// <summary>A blocker in Needs You (built, awaiting the Accept) is not finished, so the other blocker's Done wakes nobody.</summary>
    [Fact]
    public async Task OtherBlockerInNeedsYou_WakesNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem waiting = SeedBlocker(harness, "OPS-0001", TaskState.NeedsYou);
        TaskItem last = SeedBlocker(harness, "OPS-0002", TaskState.Done);
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [waiting.Id, last.Id]));

        await RaiseAndFireAsync(harness, last, cast.KaiActor, "status: In Progress → Done");

        Assert.Empty(harness.Posts);
    }
}
