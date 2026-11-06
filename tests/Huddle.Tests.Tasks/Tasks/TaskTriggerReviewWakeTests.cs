using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// #403: a Task that enters Review must reach someone who can review it, once, and the wake tells them to record the outcome on the
/// Task. Shape A: the assignee in Review reviewed in chat and recorded nothing. Shape B: the manager moved the Task to Review with the
/// builder still assignee, so nobody who can review was woken. Huddle's own move to Needs You still wakes nobody.
/// </summary>
public sealed class TaskTriggerReviewWakeTests
{
    private const string Probe = "{{assignee}}|{{taskId}}|{{actor}}|{{changes}}";

    private const string RecordSentence = "Review the work, then record the outcome on the Task and name the next step; a reply in chat alone does not move it.";

    /// <summary>Platform: manager Mia, builder Nova, reviewer Kai.</summary>
    private static FakeTeamCatalog Catalog() => new()
    {
        Teams = [new TeamSummary("Platform", [], ["Mia", "Nova", "Kai"], true) { Manager = "Mia" }],
    };

    /// <summary>Says Kai is the one member who can review.</summary>
    private sealed class FakeReviewers : IRequestConfirmers
    {
        /// <inheritdoc />
        public bool IsVerifierOf(string team, string member) => false;

        /// <inheritdoc />
        public bool TeamHasConfirmer(string team) => false;

        /// <inheritdoc />
        public bool IsChallengerOf(string team, string member) => string.Equals(member, "Kai", StringComparison.Ordinal);
    }

    private sealed record Arrangement(Harness Harness, TaskActor Manager, TaskItem Task);

    /// <summary>Builds a started trigger, a Room holding everyone, and a Task assigned to <paramref name="assignee"/>.</summary>
    private static async Task<Arrangement> ArrangeAsync(TempDataDir dir, string assignee, CancellationToken ct)
    {
        Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog(), new FakeReviewers());
        Cast cast = await StartWakingAsync(harness, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Platform"]), "You are Mia.");
        User? mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct);
        Assert.NotNull(mia);
        Room room = await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, mia.Id], ct);
        harness.Prompts.SetOverride(WakeMessageKey, Probe);
        TaskItem seeded = harness.Seed(TestTasks.Make(id: "PLAT-0001", status: TaskState.Review, assignee: assignee, originRoomId: room.Id));
        return new Arrangement(harness, new TaskActor(TaskActorKind.Agent, "Mia", mia.Id), seeded);
    }

    /// <summary>Raises a status change as the actor and lets the coalescing window elapse.</summary>
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

    private static List<string> PostsTo(Harness harness, string name) =>
        [.. harness.Posts.Select(static p => p.Message.Text).Where(text => text.StartsWith(name + "|", StringComparison.Ordinal))];

    /// <summary>Shape B: the manager moves the Task to Review with the builder assignee; the reviewer is woken once, with the record sentence.</summary>
    [Fact]
    public async Task EntersReview_AssigneeCannotReview_WakesReviewerOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Nova", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Manager, TaskState.InProgress, TaskState.Review);

        string post = Assert.Single(PostsTo(harness, "Kai"));
        Assert.Equal("Kai|PLAT-0001|Mia|- status: In Progress → Review\n" + RecordSentence, post);
        Assert.Single(PostsTo(harness, "Nova"));
    }

    /// <summary>Shape A: the assignee can review, so the one wake is theirs and says to record the outcome on the Task.</summary>
    [Fact]
    public async Task EntersReview_AssigneeCanReview_WakesAssigneeOnceWithRecordSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Kai", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Manager, TaskState.InProgress, TaskState.Review);

        string post = Assert.Single(PostsTo(harness, "Kai"));
        Assert.Equal("Kai|PLAT-0001|Mia|- status: In Progress → Review\n" + RecordSentence, post);
        Assert.Empty(PostsTo(harness, "Nova"));
    }

    /// <summary>Two entries into Review wake the reviewer twice (once each); an edit inside Review wakes no one extra.</summary>
    [Fact]
    public async Task EntersReview_Twice_WakesOncePerEntry_AndAnEditInReviewWakesNoReviewer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Nova", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Manager, TaskState.InProgress, TaskState.Review);
        await MoveAsync(arrangement, arrangement.Manager, TaskState.Review, TaskState.InProgress);
        await MoveAsync(arrangement, arrangement.Manager, TaskState.InProgress, TaskState.Review);
        TaskItem before = harness.Store.Get(arrangement.Task.Id) ?? arrangement.Task;
        harness.Raise(before, arrangement.Manager, "note: still reviewing");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(2, PostsTo(harness, "Kai").Count);
    }

    /// <summary>A move into Review by the reviewer themselves wakes no second reviewer and nobody twice.</summary>
    [Fact]
    public async Task EntersReview_ReviewerIsActor_IsNotWoken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Nova", ct);
        using Harness harness = arrangement.Harness;
        User? kai = await harness.Directory.FindUserByNameAsync("Kai", ct);
        Assert.NotNull(kai);

        await MoveAsync(arrangement, new TaskActor(TaskActorKind.Agent, kai.Name, kai.Id), TaskState.InProgress, TaskState.Review);

        Assert.Empty(PostsTo(harness, "Kai"));
    }

    /// <summary>A move to another status keeps its wake exactly as before and carries no record sentence.</summary>
    [Fact]
    public async Task MoveToInProgress_WakesNoReviewerAndAddsNoSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, "Nova", ct);
        using Harness harness = arrangement.Harness;

        await MoveAsync(arrangement, arrangement.Manager, TaskState.Review, TaskState.InProgress);

        Assert.Empty(PostsTo(harness, "Kai"));
        Assert.DoesNotContain(RecordSentence, Assert.Single(PostsTo(harness, "Nova")), StringComparison.Ordinal);
    }
}
