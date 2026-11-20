using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// SW-T3 (smart wakes spec 5.3 and corrections 1, 2): a change that wakes neither recipient is dropped on receipt. It joins no batch, resets
/// no wake budget, forgets no held wake, holds nothing and counts toward no outside-edit flood window, while a status move that unblocks
/// another Task still does, and a Request's C-5 move still reaches its requesting manager. The Platform manager is Mia, the assignee Nova and
/// the usual actor Kai, unless a test says otherwise.
/// </summary>
public sealed class TaskTriggerServiceSilentTests
{
    private const string Probe = "{{assignee}}|{{actor}}|{{changes}}";

    private static readonly FieldChange Rank = new(TaskField.Rank, "2", "1");

    private static readonly FieldChange Title = new(TaskField.Title, "Old", "New");

    /// <summary>A catalog whose Platform Team names Mia as manager.</summary>
    private static FakeTeamCatalog ManagerCatalog() =>
        new() { Teams = [new TeamSummary("Platform", [], ["Nova", "Kai", "Mia"], true) { Manager = "Mia" }] };

    /// <summary>Platform receives (manager Nova); Design requests (manager Mia).</summary>
    private static FakeTeamCatalog RequestCatalog() => new()
    {
        Teams =
        [
            new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" },
            new TeamSummary("Design", [], ["Mia"], true) { Manager = "Mia" },
        ],
    };

    /// <summary>Registers Mia as a Persona and Agent user, builds the Room {Human, Nova, Kai, Mia} and installs the probe template.</summary>
    private static async Task<Room> AddMiaAsync(Harness harness, Cast cast, CancellationToken ct)
    {
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Platform"]), "You are Mia.");
        User? mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct);
        Assert.NotNull(mia);
        harness.Prompts.SetOverride(WakeMessageKey, Probe);
        return await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, mia.Id], ct);
    }

    /// <summary>A change of <paramref name="task"/> (before and after are the same Task) whose only fields are <paramref name="fields"/>.</summary>
    private static TaskChange Change(Harness harness, TaskItem task, TaskActor actor, string summary, params FieldChange[] fields) =>
        new(task, task, fields, actor, new ChangeLogEntry(harness.Clock.GetUtcNow(), actor.Name, summary));

    /// <summary>A status move of <paramref name="task"/>, written the way the Task file writes it.</summary>
    private static TaskChange Move(Harness harness, TaskItem task, TaskActor actor, TaskState from, TaskState to) =>
        Change(harness, task, actor, $"Status: {from.ToWire()} → {to.ToWire()}", new FieldChange(TaskField.Status, from.ToWire(), to.ToWire()));

    /// <summary>Lets the coalescing window elapse and waits for every fire.</summary>
    private static async Task FireAsync(Harness harness)
    {
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
    }

    /// <summary>The Message texts posted, in order.</summary>
    private static string[] Texts(Harness harness) => [.. harness.Posts.Select(static p => p.Message.Text)];

    /// <summary>A rank-only change by an Agent posts nothing and leaves the wake budget and the last wake alone: it was dropped on receipt.</summary>
    [Fact]
    public async Task RankOnly_ByAnAgent_PostsNothingAndLeavesTheWakeBudgetUntouched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, ManagerCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddMiaAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Events.RaiseTaskChanged(Change(harness, task, cast.KaiActor, "Rank: 2 → 1", Rank));
        await FireAsync(harness);

        Assert.Empty(harness.Posts);
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
        Assert.Null(harness.Activity.LastWake(task.Id));
    }

    /// <summary>A rank-only change by an Agent on a Task whose wake budget is spent holds nothing: there is no wake to hold.</summary>
    [Fact]
    public async Task RankOnly_ByAnAgentWithTheBudgetSpent_HoldsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Events.RaiseTaskChanged(Change(harness, task, cast.KaiActor, "Rank: 2 → 1", Rank));
        await FireAsync(harness);

        Assert.False(harness.Trigger.IsHeld(task.Id));
        Assert.Empty(harness.Posts);
    }

    /// <summary>
    /// Correction 2: a rank-only change by the Human leaves a held wake held, leaves the spent budget spent and posts nothing, because a dropped
    /// change runs neither <c>ResetForHuman</c> nor <c>ForgetHeld</c>. A Human change that is kept (a Title edit) then does both, as today.
    /// </summary>
    [Fact]
    public async Task RankOnly_ByTheHuman_LeavesAHeldWakeHeld_ButAKeptHumanChangeClearsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, Probe);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));
        harness.Events.RaiseTaskChanged(Change(harness, task, cast.KaiActor, "Title: Old → New", Title));
        await FireAsync(harness);
        Assert.True(harness.Trigger.IsHeld(task.Id));

        harness.Events.RaiseTaskChanged(Change(harness, task, HumanActor, "Rank: 2 → 1", Rank));
        await FireAsync(harness);

        Assert.True(harness.Trigger.IsHeld(task.Id));
        Assert.True(harness.Activity.Budget(task.Id).Exhausted);
        Assert.Empty(harness.Posts);

        harness.Events.RaiseTaskChanged(Change(harness, task, HumanActor, "Title: Old → New", Title));
        await FireAsync(harness);

        Assert.False(harness.Trigger.IsHeld(task.Id));
        Assert.False(harness.Activity.Budget(task.Id).Exhausted);
        Assert.Equal(["Nova|You|- Title: Old → New"], Texts(harness));
    }

    /// <summary>
    /// A silent outside edit is not counted toward the flood window: ten rank-only edits and one Title edit made outside Huddle in one window
    /// post the one Message for the Title edit, where counting all eleven would have skipped it as the one that tipped the window over.
    /// </summary>
    [Fact]
    public async Task SilentOutsideEdits_AreNotCountedTowardTheFloodWindow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}|{{changes}}");
        TaskItem[] tasks = SeedNumbered(harness, cast, count: 11);

        foreach (TaskItem task in tasks[..10])
        {
            harness.Events.RaiseTaskChanged(Change(harness, task, OutsideActor, "Rank: 2 → 1", Rank));
        }

        harness.Events.RaiseTaskChanged(Change(harness, tasks[10], OutsideActor, "Title: Old → New", Title));
        await FireAsync(harness);

        Assert.Equal(["PLAT-0011|- Title: Old → New"], Texts(harness));
    }

    /// <summary>A silent change and then a waking one in one window post one Message per recipient, listing only the waking change.</summary>
    [Fact]
    public async Task SilentThenWakingChange_InOneWindow_PostsOneMessageListingOnlyTheWakingChange()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, ManagerCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddMiaAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Change(harness, task, cast.KaiActor, "Rank: 2 → 1", Rank));
        harness.Events.RaiseTaskChanged(Change(harness, task, cast.KaiActor, "Title: Old → New", Title));
        await FireAsync(harness);

        Assert.Equal(["Nova|Kai|- Title: Old → New", "Mia|Kai|- Title: Old → New"], Texts(harness));
    }

    /// <summary>
    /// From slice 2 a comment wakes only the Agents it Mentions (correction 11): a silent change and then a comment Mentioning Nova in one window post
    /// one Message, to Nova alone (not to the manager Mia), listing only the comment, so the comment is neither dropped nor does it drag the silent
    /// change into the Message. Changed from <c>SilentThenComment_InOneWindow_PostsOneMessageListingOnlyTheComment</c>, which expected a Message to each
    /// recipient for a comment that Mentioned no one.
    /// </summary>
    [Fact]
    public async Task SilentThenMentioningComment_InOneWindow_PostsOneMessageToTheMentionedAgentListingOnlyTheComment()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, ManagerCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddMiaAsync(harness, cast, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id));
        TaskChange comment = new(
            task,
            task,
            [],
            cast.KaiActor,
            new ChangeLogEntry(harness.Clock.GetUtcNow(), "Kai", "comment added by Kai"),
            Kind: TaskChangeKind.Comment,
            Comment: "Ready for another look, @Nova.");

        harness.Events.RaiseTaskChanged(Change(harness, task, cast.KaiActor, "Rank: 2 → 1", Rank));
        harness.Events.RaiseTaskChanged(comment);
        await FireAsync(harness);

        Assert.Equal(["Nova|Kai|- comment by Kai: \"Ready for another look, " + NeutralisedAt + "Nova.\""], Texts(harness));
    }

    /// <summary>
    /// The status table (spec 3.4) decides each recipient separately, the assignee Nova and the manager Mia, for a move by Kai: To Do to In
    /// Progress wakes the assignee only, In Progress to Cancelled wakes both, Backlog to Cancelled wakes no one.
    /// </summary>
    /// <param name="from">The old status.</param>
    /// <param name="to">The new status.</param>
    /// <param name="expected">The Agents woken, in order, comma-separated; empty for no one.</param>
    [Theory]
    [InlineData(TaskState.ToDo, TaskState.InProgress, "Nova")]
    [InlineData(TaskState.Backlog, TaskState.ToDo, "Nova")]
    [InlineData(TaskState.NeedsYou, TaskState.InProgress, "Nova")]
    [InlineData(TaskState.InProgress, TaskState.NeedsYou, "")]
    [InlineData(TaskState.InProgress, TaskState.Cancelled, "Nova,Mia")]
    [InlineData(TaskState.InProgress, TaskState.Done, "Nova,Mia")]
    [InlineData(TaskState.Done, TaskState.InProgress, "Nova,Mia")]
    [InlineData(TaskState.Backlog, TaskState.Cancelled, "")]
    [InlineData(TaskState.Backlog, TaskState.Done, "")]
    [InlineData(TaskState.Done, TaskState.Cancelled, "")]
    public async Task StatusMove_WakesTheRecipientsOfTheTable(TaskState from, TaskState to, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, ManagerCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddMiaAsync(harness, cast, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}");
        TaskItem task = harness.Seed(TestTasks.Make(status: to, assignee: "Nova", originRoomId: room.Id));

        harness.Events.RaiseTaskChanged(Move(harness, task, cast.KaiActor, from, to));
        await FireAsync(harness);

        Assert.Equal(expected, string.Join(',', Texts(harness)));
    }

    /// <summary>
    /// Correction 1: a blocker that moves from Backlog to Cancelled is a silent move for its own assignee, so it posts nothing for them, but it
    /// still reaches a finished status and so still wakes the Task it blocked (<c>StartUnblock</c> runs before the receipt drop).
    /// </summary>
    [Fact]
    public async Task BacklogToCancelled_PostsNothingForTheBlockerButStillWakesTheTaskItBlocked()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}|{{taskId}}");
        TaskItem blocker = harness.Seed(TestTasks.Make(id: "OPS-0001", status: TaskState.Cancelled, assignee: "Kai", originRoomId: cast.Room.Id, location: new TaskLocation("Ops", null, false)));
        _ = harness.Seed(TestTasks.Make(id: "PLAT-0007", assignee: "Nova", originRoomId: cast.Room.Id, blockedBy: [blocker.Id]));

        harness.Events.RaiseTaskChanged(Move(harness, blocker, HumanActor, TaskState.Backlog, TaskState.Cancelled));
        await FireAsync(harness);

        Assert.Equal(["Nova|PLAT-0007"], Texts(harness));
    }

    /// <summary>
    /// A Request moved from Backlog to Rejected (C-5) wakes its requesting manager only: the assignee and the receiving Team's manager are
    /// silent for a Planning to Won't do move, but the move still reaches the batch because it is a Request status move.
    /// </summary>
    [Fact]
    public async Task Request_BacklogToRejected_WakesOnlyTheRequestingManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, RequestCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddMiaAsync(harness, cast, ct);
        TaskItem request = harness.Seed(TestTasks.Make(
            id: "PLAT-0001",
            status: TaskState.Rejected,
            assignee: "Kai",
            originRoomId: room.Id,
            unknownFields: [.. new RequestFields(RequestKind.Bug, "Design", "Mia", TriageState.None, string.Empty, string.Empty).ToFrontmatter()]));

        harness.Events.RaiseTaskChanged(Move(harness, request, HumanActor, TaskState.Backlog, TaskState.Rejected));
        await FireAsync(harness);

        Assert.Equal(["Mia|You|- Status: Backlog → Rejected"], Texts(harness));
    }

    /// <summary>
    /// A Request moved from Done to Duplicate is silent for everyone, the requesting manager included (correction 16): a move from one
    /// finished status to another is a relabel.
    /// </summary>
    [Fact]
    public async Task Request_DoneToDuplicate_WakesNoOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, RequestCatalog());
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddMiaAsync(harness, cast, ct);
        TaskItem request = harness.Seed(TestTasks.Make(
            id: "PLAT-0001",
            status: TaskState.Duplicate,
            duplicateOf: new TaskId("PLAT", 2),
            assignee: "Kai",
            originRoomId: room.Id,
            unknownFields: [.. new RequestFields(RequestKind.Bug, "Design", "Mia", TriageState.None, string.Empty, string.Empty).ToFrontmatter()]));

        harness.Events.RaiseTaskChanged(Move(harness, request, HumanActor, TaskState.Done, TaskState.Duplicate));
        await FireAsync(harness);

        Assert.Empty(harness.Posts);
    }
}
