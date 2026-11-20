using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Request wake-ups (Task 71, RQ-4, RQ-7): <see cref="RequestNotifier"/> maps each <see cref="WakeTarget"/> to a Persona and wakes it,
/// and <see cref="TaskTriggerService"/> wakes the requesting Team's manager on every status change of a Request.
/// </summary>
public sealed class RequestWakeTests
{
    private const string Probe = "{{assignee}}|{{taskId}}|{{actor}}|{{changes}}";

    /// <summary>Platform receives (manager Nova unless <paramref name="platformManager"/> is false); Design requests (manager Mia unless <paramref name="designManager"/> is false).</summary>
    private static FakeTeamCatalog Catalog(bool platformManager = true, bool designManager = true) => new()
    {
        Teams =
        [
            new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = platformManager ? "Nova" : null },
            new TeamSummary("Design", [], ["Mia"], true) { Manager = designManager ? "Mia" : null },
        ],
    };

    /// <summary>Registers Mia (Design manager) and Alfred (Chief of Staff) as Personas and Agent users and builds a Room all of them share.</summary>
    private static async Task<Room> AddCastAsync(Harness harness, Cast cast, CancellationToken ct)
    {
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Design"]), "You are Mia.");
        _ = harness.Personas.Add(new PersonaIdentity("Alfred", "Chief of Staff", "alf", [], Builtin: BuiltinAgent.ChiefOfStaffMarker), "You are Alfred.");
        User? mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct);
        User? alfred = await harness.Directory.UpsertAgentUserAsync("Alfred", null, ct);
        Assert.NotNull(mia);
        Assert.NotNull(alfred);
        harness.Prompts.SetOverride(WakeMessageKey, Probe);
        return await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, mia.Id, alfred.Id], ct);
    }

    /// <summary>A Request filed by Design to Platform, or an ordinary Task when <paramref name="isRequest"/> is false.</summary>
    private static TaskItem Request(Harness harness, Room room, string fromTeam = "Design", bool isRequest = true) =>
        harness.Seed(TestTasks.Make(
            id: "PLAT-0001",
            originRoomId: room.Id,
            unknownFields: isRequest ? [.. new RequestFields(RequestKind.Bug, fromTeam, "Mia", TriageState.None, string.Empty, string.Empty).ToFrontmatter()] : []));

    /// <summary>Raises a status change of <paramref name="task"/> by <paramref name="actor"/> and lets its window elapse.</summary>
    private static async Task ChangeStatusAsync(Harness harness, TaskItem task, TaskActor actor)
    {
        harness.Events.RaiseTaskChanged(new TaskChange(
            task,
            task,
            [new FieldChange(TaskField.Status, "In Progress", "Done")],
            actor,
            new ChangeLogEntry(harness.Clock.GetUtcNow(), actor.Name, "Status: In Progress → Done")));
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
    }

    /// <summary>Builds the notifier over the harness and the catalog.</summary>
    private static RequestNotifier Notifier(Harness harness, FakeTeamCatalog catalog, RecordingLogger<RequestNotifier>? logger = null) =>
        new(harness.Trigger, catalog, harness.Personas, logger ?? new RecordingLogger<RequestNotifier>());

    /// <summary>The requesting-manager target wakes the manager of the Team that filed the Request, with a message naming the Task, the event and the actor.</summary>
    [Fact]
    public async Task Notify_RequestingManager_WakesFromTeamManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = Catalog();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddCastAsync(harness, cast, ct);
        TaskItem task = Request(harness, room);

        await Notifier(harness, catalog).NotifyAsync(task, RequestEvent.Accept, [WakeTarget.RequestingManager], "Nova", ct);

        Assert.Equal(["Mia|PLAT-0001|Nova|- Request event: Accept"], harness.Posts.Select(p => p.Message.Text).ToArray());
    }

    /// <summary>The receiving-manager target wakes the manager of the Task's own Team.</summary>
    [Fact]
    public async Task Notify_ReceivingManager_WakesTaskTeamManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = Catalog();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddCastAsync(harness, cast, ct);
        TaskItem task = Request(harness, room);

        await Notifier(harness, catalog).NotifyAsync(task, RequestEvent.Filed, [WakeTarget.ReceivingManager], "Mia", ct);

        Assert.Equal(["Nova|PLAT-0001|Mia|- Request event: Filed"], harness.Posts.Select(p => p.Message.Text).ToArray());
    }

    /// <summary>The Chief of Staff target wakes the built-in Agent, found by its marker rather than its name.</summary>
    [Fact]
    public async Task Notify_ChiefOfStaff_WakesBuiltinAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = Catalog();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddCastAsync(harness, cast, ct);
        TaskItem task = Request(harness, room);

        await Notifier(harness, catalog).NotifyAsync(task, RequestEvent.Confirm, [WakeTarget.ChiefOfStaff], "Nova", ct);

        Assert.Equal(["Alfred|PLAT-0001|Nova|- Request event: Confirm"], harness.Posts.Select(p => p.Message.Text).ToArray());
    }

    /// <summary>A decline wakes both the requesting manager and the Chief of Staff, in the table's order.</summary>
    [Fact]
    public async Task Notify_Decline_WakesRequestingManagerAndChiefOfStaff()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = Catalog();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddCastAsync(harness, cast, ct);
        TaskItem task = Request(harness, room);

        await Notifier(harness, catalog).NotifyAsync(task, RequestEvent.Decline, [WakeTarget.RequestingManager, WakeTarget.ChiefOfStaff], "Nova", ct);

        Assert.Equal(
            ["Mia|PLAT-0001|Nova|- Request event: Decline", "Alfred|PLAT-0001|Nova|- Request event: Decline"],
            harness.Posts.Select(p => p.Message.Text).ToArray());
    }

    /// <summary>A target that resolves to nobody wakes nobody and logs which target it was.</summary>
    [Fact]
    public async Task Notify_RequestingTeamWithoutManager_WakesNobodyAndLogs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = Catalog(designManager: false);
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddCastAsync(harness, cast, ct);
        TaskItem task = Request(harness, room);
        RecordingLogger<RequestNotifier> logger = new();

        await Notifier(harness, catalog, logger).NotifyAsync(task, RequestEvent.Accept, [WakeTarget.RequestingManager], "Nova", ct);

        Assert.Empty(harness.Posts);
        Assert.Equal("Request PLAT-0001: nobody to wake for RequestingManager.", Assert.Single(logger.Entries).Message);
    }

    /// <summary>A status change by the receiving Team wakes the requesting Team's manager when it is a C-5 move (In Progress to Done); the receiving Team's own wake is silent here because the Task has no assignee and its Team has no manager.</summary>
    [Fact]
    public async Task StatusChange_ByReceivingTeam_WakesRequestingManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog(platformManager: false));
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddCastAsync(harness, cast, ct);
        TaskItem task = Request(harness, room);

        await ChangeStatusAsync(harness, task, cast.KaiActor);

        Assert.Equal(["Mia|PLAT-0001|Kai|- Status: In Progress → Done"], harness.Posts.Select(p => p.Message.Text).ToArray());
    }

    /// <summary>The requesting manager's own status change does not wake that manager.</summary>
    [Fact]
    public async Task StatusChange_ByRequestingManager_WakesNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog(platformManager: false));
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddCastAsync(harness, cast, ct);
        User? mia = await harness.Directory.FindUserByNameAsync("Mia", ct);
        Assert.NotNull(mia);
        TaskItem task = Request(harness, room);

        await ChangeStatusAsync(harness, task, new TaskActor(TaskActorKind.Agent, "Mia", mia.Id));

        Assert.Empty(harness.Posts);
    }

    /// <summary>A change to a Task with no <c>request</c> key does not wake any requesting manager.</summary>
    [Fact]
    public async Task StatusChange_OfOrdinaryTask_WakesNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog(platformManager: false));
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddCastAsync(harness, cast, ct);
        TaskItem task = Request(harness, room, isRequest: false);

        await ChangeStatusAsync(harness, task, cast.KaiActor);

        Assert.Empty(harness.Posts);
    }

    /// <summary>A Request whose <c>from_team</c> has no manager wakes nobody and logs it.</summary>
    [Fact]
    public async Task StatusChange_RequestingTeamWithoutManager_WakesNobodyAndLogs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog(platformManager: false, designManager: false));
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddCastAsync(harness, cast, ct);
        TaskItem task = Request(harness, room);

        await ChangeStatusAsync(harness, task, cast.KaiActor);

        Assert.Empty(harness.Posts);
        Assert.Contains(
            harness.Logger.Entries,
            entry => entry.Message == "Not waking the requesting manager of Request PLAT-0001: team 'Design' has no manager.");
    }
}
