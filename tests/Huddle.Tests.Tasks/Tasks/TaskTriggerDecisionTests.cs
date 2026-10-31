using Agency.Huddle.App;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// The wake <see cref="TaskTriggerService"/> posts when the Human decides a Task (Human gate Spec §6.6):
/// the decision sentence is its own last line, after whatever the wake template renders, and the
/// Spec §10.2 guards still apply.
/// </summary>
public sealed class TaskTriggerDecisionTests
{
    /// <summary>Accept on a Task with an Action wakes the assignee with the go-ahead sentence naming the Action and the Task.</summary>
    [Fact]
    public async Task Decision_AcceptWithAction_WakesWithTheGoAheadSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id) with { Action = "deploy to production" });

        RaiseDecision(harness, task, HumanActor, TaskDecision.Accept, null);
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.EndsWith("\nAccepted: go ahead and deploy to production, then move PLAT-0001 to Done.", post.Message.Text, StringComparison.Ordinal); // contains-ok: the decision sentence is the Message's last line; the template above it varies.
    }

    /// <summary>Request changes quotes the Human's note.</summary>
    [Fact]
    public async Task Decision_RequestChanges_QuotesTheNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        RaiseDecision(harness, task, HumanActor, TaskDecision.RequestChanges, "cover the retry path");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.EndsWith("\nChanges requested: \"cover the retry path\"", post.Message.Text, StringComparison.Ordinal); // contains-ok: the decision sentence is the Message's last line; the template above it varies.
    }

    /// <summary>Decline quotes the Human's reason.</summary>
    [Fact]
    public async Task Decision_Decline_QuotesTheReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        RaiseDecision(harness, task, HumanActor, TaskDecision.Decline, "we are not building this");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.EndsWith("\nDeclined: \"we are not building this\"", post.Message.Text, StringComparison.Ordinal); // contains-ok: the decision sentence is the Message's last line; the template above it varies.
    }

    /// <summary>Spec §10.2 guard 4 still applies: a decision whose actor is the assignee wakes nobody.</summary>
    [Fact]
    public async Task Decision_ActorIsTheAssignee_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        RaiseDecision(harness, task, cast.NovaActor, TaskDecision.RequestChanges, "my own note");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
    }

    /// <summary>Spec §10.3 coalescing still applies: a decision and a second change by the Human in one window are one Message listing both.</summary>
    [Fact]
    public async Task Decision_CoalescedWithAnotherChange_IsOneMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        RaiseDecision(harness, task, HumanActor, TaskDecision.RequestChanges, "cover the retry path");
        harness.Raise(task, HumanActor, "Priority: Medium → High");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Contains("- Priority: Medium → High", post.Message.Text, StringComparison.Ordinal); // contains-ok: one line of the coalesced change list; the list's order and wording are TaskTriggerService's existing template.
        Assert.EndsWith("\nChanges requested: \"cover the retry path\"", post.Message.Text, StringComparison.Ordinal); // contains-ok: the decision sentence is the Message's last line; the template above it varies.
    }

    /// <summary>Raises a change that carries a Human decision and its note.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="after">The Task after the decision.</param>
    /// <param name="actor">Who decided.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="note">The note, or <see langword="null"/>.</param>
    private static void RaiseDecision(Harness harness, TaskItem after, TaskActor actor, TaskDecision decision, string? note) =>
        harness.Events.RaiseTaskChanged(new TaskChange(
            null,
            after,
            [],
            actor,
            new ChangeLogEntry(harness.Clock.GetUtcNow(), actor.Name, note ?? "accepted"),
            decision,
            note));
}
