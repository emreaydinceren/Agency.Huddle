using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks.HumanGate;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Huddle's own move of a ready gated Task to Needs You (#349 Part 2) wakes nobody: not the assignee, not the Team's manager and
/// not, on a Request, the manager of the Team that filed it. It spends no wake budget and still tells the Human through the Feed,
/// and a later move of the same Request to Done still wakes the requesting manager (#350).
/// </summary>
public sealed class TaskTriggerNeedsYouMoveTests
{
    private const string Probe = "{{assignee}}|{{taskId}}|{{actor}}|{{changes}}";

    /// <summary>Platform receives (manager Nova); Design requests (manager Mia).</summary>
    private static FakeTeamCatalog Catalog() => new()
    {
        Teams =
        [
            new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" },
            new TeamSummary("Design", [], ["Mia"], true) { Manager = "Mia" },
        ],
    };

    /// <summary>Everything one test arranges: the trigger harness, the real Task service over its store, and the Proof.</summary>
    private sealed record Arrangement(Harness Harness, TaskService Service, FixedProofSource Proof, TaskItem Task, Cast Cast);

    /// <summary>Builds a gated Request in Review assigned to Nova, whose Proof is open, over a started trigger service.</summary>
    /// <param name="dir">The data directory.</param>
    /// <param name="ct">Cancels the arrangement.</param>
    private static async Task<Arrangement> ArrangeAsync(TempDataDir dir, CancellationToken ct)
    {
        Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog());
        Cast cast = await StartWakingAsync(harness, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Design"]), "You are Mia.");
        User? mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct);
        Assert.NotNull(mia);
        Room room = await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, mia.Id], ct);
        harness.Prompts.SetOverride(WakeMessageKey, Probe);

        TaskItem seeded = harness.Seed(TestTasks.Make(
            id: "PLAT-0001",
            status: TaskState.Review,
            assignee: "Nova",
            originRoomId: room.Id,
            unknownFields: [.. new RequestFields(RequestKind.Bug, "Design", "Mia", TriageState.None, string.Empty, string.Empty).ToFrontmatter()]) with { HumanGate = true });

        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        IOptions<TeamOptions> options = dir.Options();
        TaskService service = new(
            harness.Store, new TaskIdAllocator(options), harness.Events, harness.Personas, new EmptySprintCatalog(), options,
            TimeProvider.System, NullLogger<TaskService>.Instance, proof: proof, feed: harness.Feed);
        return new Arrangement(harness, service, proof, seeded, cast);
    }

    /// <summary>Nova adds the Evidence that completes the Proof, then the coalescing window elapses.</summary>
    /// <param name="arrangement">The arrangement.</param>
    private static async Task CompleteProofAsync(Arrangement arrangement)
    {
        arrangement.Proof.Current = FixedProofSource.Complete;
        EvidenceLine line = new(DateTimeOffset.UtcNow, "Nova", "dotnet test", 0, null, null, "raw");
        _ = Assert.IsType<TaskResult.Saved>(arrangement.Service.AddEvidence(arrangement.Task.Id, line, arrangement.Cast.NovaActor));
        arrangement.Harness.Clock.Advance(DefaultWindow);
        await arrangement.Harness.Trigger.WhenIdleAsync();
    }

    /// <summary>The move wakes neither the assignee, nor the manager, nor the requesting manager of a gated Request.</summary>
    [Fact]
    public async Task Move_ToNeedsYou_WakesNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, ct);
        using Harness harness = arrangement.Harness;

        await CompleteProofAsync(arrangement);

        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(arrangement.Task.Id)?.Status);
        Assert.Empty(harness.Posts);
    }

    /// <summary>The move spends none of the Task's wake budget and records no wake.</summary>
    [Fact]
    public async Task Move_ToNeedsYou_UsesNoWakeBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, ct);
        using Harness harness = arrangement.Harness;

        await CompleteProofAsync(arrangement);

        Assert.Equal(0, harness.Activity.Budget(arrangement.Task.Id).Used);
        Assert.Null(harness.Activity.LastWake(arrangement.Task.Id));
    }

    /// <summary>The move posts the Human's TaskNeedsYou Feed entry with the Proof count.</summary>
    [Fact]
    public async Task Move_ToNeedsYou_PostsTaskNeedsYou()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, ct);
        using Harness harness = arrangement.Harness;

        await CompleteProofAsync(arrangement);

        FeedMessage post = Assert.Single(harness.Feed.Posts, static message => message.SourceType == FeedSourceType.TaskNeedsYou);
        TaskNeedsYouPayload payload = Assert.IsType<TaskNeedsYouPayload>(post.Payload);
        Assert.Equal((4, 4), (payload.ProofMet, payload.ProofTotal));
    }

    /// <summary>Suppressing the move's wake does not silence the Request: its later move to Done still wakes the requesting manager.</summary>
    [Fact]
    public async Task Request_MovedToDoneAfterTheMove_StillWakesRequestingManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Arrangement arrangement = await ArrangeAsync(dir, ct);
        using Harness harness = arrangement.Harness;
        await CompleteProofAsync(arrangement);
        TaskItem waiting = harness.Store.Get(arrangement.Task.Id) ?? arrangement.Task;

        harness.Events.RaiseTaskChanged(new TaskChange(
            waiting,
            waiting,
            [new FieldChange(TaskField.Status, "Needs You", "Done")],
            arrangement.Cast.NovaActor,
            new ChangeLogEntry(harness.Clock.GetUtcNow(), "Nova", "status: Needs You → Done")));
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        string post = Assert.Single(harness.Posts).Message.Text;
        Assert.Equal("Mia|PLAT-0001|Nova|- status: Needs You → Done", post);
    }
}
