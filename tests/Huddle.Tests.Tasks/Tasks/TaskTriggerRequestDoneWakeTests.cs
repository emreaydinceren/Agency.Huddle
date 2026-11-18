using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// #350 item 3: when a Request reaches Done, the wake that reaches the requesting Team's manager says it is their turn to confirm.
/// A Request's other moves and a plain Task's move to Done carry no such line, and the skip rules (the requesting manager made the
/// change, or is the assignee or the receiving manager) still leave someone told. D15's suppression of Huddle's own move to
/// Needs You is unchanged: that move still wakes nobody.
/// </summary>
public sealed class TaskTriggerRequestDoneWakeTests
{
    private const string Probe = "{{assignee}}|{{taskId}}|{{actor}}|{{changes}}";

    /// <summary>The confirm line, with the verifiers filled in.</summary>
    private static string ConfirmLine(string verifiers) =>
        $"PLAT-0001 is a Request and it is now Done. It waits for your Team to confirm it. Only a verifier on your Team can call confirm_request; yours: {verifiers}. If that is you, check it and confirm it. If not, ask them to.";

    /// <summary>Platform receives (manager Nova, Kai); Design requests (manager Mia, QA Quinn).</summary>
    private static FakeTeamCatalog Catalog() => new()
    {
        Teams =
        [
            new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" },
            new TeamSummary("Design", [], ["Mia", "Quinn"], true) { Manager = "Mia" },
        ],
    };

    /// <summary>Says who verifies: Quinn for Design (the development shape, the manager does not), Nova for Platform (the small-business shape, the manager does).</summary>
    private sealed class FakeConfirmers : IRequestConfirmers
    {
        /// <summary>Whether Design has a verifier at all.</summary>
        public bool DesignHasVerifier { get; init; } = true;

        /// <inheritdoc />
        public bool IsVerifierOf(string team, string member) =>
            (string.Equals(team, "Design", StringComparison.Ordinal) && this.DesignHasVerifier && string.Equals(member, "Quinn", StringComparison.Ordinal))
            || (string.Equals(team, "Platform", StringComparison.Ordinal) && string.Equals(member, "Nova", StringComparison.Ordinal));

        /// <inheritdoc />
        public bool TeamHasConfirmer(string team) => Catalog().Find(team)?.Members.Any(member => this.IsVerifierOf(team, member)) ?? false;
    }

    /// <summary>Everything one test arranges.</summary>
    private sealed record Arrangement(Harness Harness, Cast Cast, TaskItem Task);

    /// <summary>Builds a started trigger, a Room holding everyone, and one Task In Progress.</summary>
    /// <param name="dir">The data directory.</param>
    /// <param name="assignee">The Task's assignee.</param>
    /// <param name="fromTeam">The Team that filed it, or <see langword="null"/> for a plain Task.</param>
    /// <param name="ct">Cancels the arrangement.</param>
    /// <param name="stored">The status the store holds when the wake fires, which is the status the move ends in.</param>
    /// <param name="designHasVerifier">Whether Design has a member who may confirm.</param>
    private static async Task<Arrangement> ArrangeAsync(TempDataDir dir, string assignee, string? fromTeam, CancellationToken ct, TaskState stored = TaskState.Done, bool designHasVerifier = true)
    {
        Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog(), new FakeConfirmers { DesignHasVerifier = designHasVerifier });
        Cast cast = await StartWakingAsync(harness, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Design"]), "You are Mia.");
        User? mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct);
        Assert.NotNull(mia);
        Room room = await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, mia.Id], ct);
        harness.Prompts.SetOverride(WakeMessageKey, Probe);

        string filer = fromTeam == "Design" ? "Mia" : "Nova";
        List<KeyValuePair<string, string>> pairs = [];
        if (fromTeam is not null)
        {
            pairs = [.. new RequestFields(RequestKind.Bug, fromTeam, filer, TriageState.Accepted, string.Empty, string.Empty).ToFrontmatter()];
        }

        TaskItem seeded = harness.Seed(TestTasks.Make(id: "PLAT-0001", status: stored, assignee: assignee, originRoomId: room.Id, unknownFields: pairs));
        return new Arrangement(harness, cast, seeded);
    }

    /// <summary>Raises a status change as the actor and lets the coalescing window elapse.</summary>
    /// <param name="arrangement">The arrangement.</param>
    /// <param name="actor">Who moves it.</param>
    /// <param name="from">The old status.</param>
    /// <param name="to">The new status.</param>
    private static async Task MoveAsync(Arrangement arrangement, TaskActor actor, TaskState from, TaskState to)
    {
        Harness harness = arrangement.Harness;
        TaskItem before = harness.Store.Get(arrangement.Task.Id) ?? arrangement.Task;
        TaskItem after = before with { Status = to };
        harness.Events.RaiseTaskChanged(new TaskChange(
            before with { Status = from },
            after,
            [new FieldChange(TaskField.Status, from.ToWire(), to.ToWire())],
            actor,
            new ChangeLogEntry(harness.Clock.GetUtcNow(), actor.Name, $"status: {from.ToWire()} → {to.ToWire()}")));
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
    }

    /// <summary>A Request reaching Done wakes the requesting manager with the change and the line saying it is their turn to confirm.</summary>
    [Fact]
    public async Task Request_ReachesDone_WakesRequestingManagerWithConfirmLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Kai", "Design", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.KaiActor, TaskState.InProgress, TaskState.Done);

        string post = harness.Posts.Select(static p => p.Message.Text).Single(static text => text.StartsWith("Mia|", StringComparison.Ordinal));
        Assert.Equal("Mia|PLAT-0001|Kai|- status: In Progress → Done\n" + ConfirmLine("Quinn"), post);
    }

    /// <summary>The development shape: the requesting manager is not a verifier, so the line names the QA to pass the Request to.</summary>
    [Fact]
    public async Task Request_ReachesDone_ManagerNotVerifier_LineNamesTheVerifier()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Kai", "Design", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.KaiActor, TaskState.InProgress, TaskState.Done);

        string post = harness.Posts.Select(static p => p.Message.Text).Single(static text => text.StartsWith("Mia|", StringComparison.Ordinal));
        Assert.Equal("Mia|PLAT-0001|Kai|- status: In Progress → Done\n" + ConfirmLine("Quinn"), post);    }

    /// <summary>The small-business shape: the requesting manager covers verify, so the line names the manager itself.</summary>
    [Fact]
    public async Task Request_ReachesDone_ManagerIsVerifier_LineNamesTheManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Mia", "Platform", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.KaiActor, TaskState.InProgress, TaskState.Done);

        string post = harness.Posts.Select(static p => p.Message.Text).First(static text => text.StartsWith("Nova|", StringComparison.Ordinal));
        Assert.Equal("Nova|PLAT-0001|Kai|- status: In Progress → Done\n" + ConfirmLine("Nova"), post);
    }

    /// <summary>A Team with nobody who may confirm says so, so the manager is not told to pass it to a person who does not exist.</summary>
    [Fact]
    public async Task Request_ReachesDone_NoVerifier_LineSaysNobodyCan()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Kai", "Design", ct, designHasVerifier: false);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.KaiActor, TaskState.InProgress, TaskState.Done);

        string post = harness.Posts.Select(static p => p.Message.Text).Single(static text => text.StartsWith("Mia|", StringComparison.Ordinal));
        Assert.Equal("Mia|PLAT-0001|Kai|- status: In Progress → Done\n" + ConfirmLine("nobody yet, so the Human closes it"), post);
    }

    /// <summary>The assignee on the receiving side is never told it is their turn to confirm.</summary>
    [Fact]
    public async Task Request_ReachesDone_ReceivingAssignee_IsNotToldToConfirm()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Kai", "Design", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.NovaActor, TaskState.InProgress, TaskState.Done);

        string kaiPost = harness.Posts.Select(static p => p.Message.Text).Single(static text => text.StartsWith("Kai|", StringComparison.Ordinal));
        Assert.Equal("Kai|PLAT-0001|Nova|- status: In Progress → Done", kaiPost);
    }

    /// <summary>A Request's move to a status other than Done wakes the requesting manager with the plain change and no confirm line.</summary>
    [Fact]
    public async Task Request_MovesToInProgress_WakesWithoutConfirmLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Kai", "Design", ct, TaskState.InProgress);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.KaiActor, TaskState.ToDo, TaskState.InProgress);

        string post = harness.Posts.Select(static p => p.Message.Text).Single(static text => text.StartsWith("Mia|", StringComparison.Ordinal));
        Assert.Equal("Mia|PLAT-0001|Kai|- status: To Do → In Progress", post);
    }

    /// <summary>A plain Task reaching Done wakes its assignee as before: the text is the Task wake and carries no confirm line.</summary>
    [Fact]
    public async Task PlainTask_ReachesDone_CarriesNoConfirmLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Nova", null, ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.KaiActor, TaskState.InProgress, TaskState.Done);

        string post = Assert.Single(harness.Posts).Message.Text;
        Assert.Equal("Nova|PLAT-0001|Kai|- status: In Progress → Done", post);
    }

    /// <summary>The requesting manager is also the assignee: the assignee wake is the one that tells them, and it carries the line.</summary>
    [Fact]
    public async Task Request_RequestingManagerIsAssignee_IsToldToConfirm()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Mia", "Design", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.KaiActor, TaskState.InProgress, TaskState.Done);

        string post = harness.Posts.Select(static p => p.Message.Text).First(static text => text.StartsWith("Mia|", StringComparison.Ordinal));
        Assert.Equal("Mia|PLAT-0001|Kai|- status: In Progress → Done\n" + ConfirmLine("Quinn"), post);
    }

    /// <summary>The requesting manager is also the receiving Team's manager: the manager wake is the one that tells them, and it carries the line.</summary>
    [Fact]
    public async Task Request_RequestingManagerIsReceivingManager_IsToldToConfirm()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Kai", "Platform", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.KaiActor, TaskState.InProgress, TaskState.Done);

        string post = harness.Posts.Select(static p => p.Message.Text).First(static text => text.StartsWith("Nova|", StringComparison.Ordinal));
        Assert.Equal("Nova|PLAT-0001|Kai|- status: In Progress → Done\n" + ConfirmLine("Nova"), post);
    }

    /// <summary>The requesting manager made the move itself: nobody is woken about it, and nobody else is told to confirm.</summary>
    [Fact]
    public async Task Request_RequestingManagerMadeTheMove_WakesNoOneToConfirm()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Kai", "Platform", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.NovaActor, TaskState.InProgress, TaskState.Done);

        Assert.DoesNotContain(harness.Posts.Select(static p => p.Message.Text), static text => text.EndsWith(ConfirmLine("Nova"), StringComparison.Ordinal));
    }

    /// <summary>An Agent's move of a Request to Needs You is a status change like any other: the requesting manager hears of it, without the confirm line. (Huddle's own ready move is pinned by TaskTriggerNeedsYouMoveTests.)</summary>
    [Fact]
    public async Task Request_MovesToNeedsYou_CarriesNoConfirmLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Kai", "Design", ct, TaskState.NeedsYou);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Cast.KaiActor, TaskState.InProgress, TaskState.NeedsYou);

        string post = harness.Posts.Select(static p => p.Message.Text).Single(static text => text.StartsWith("Mia|", StringComparison.Ordinal));
        Assert.Equal("Mia|PLAT-0001|Kai|- status: In Progress → Needs You", post);
    }
}
