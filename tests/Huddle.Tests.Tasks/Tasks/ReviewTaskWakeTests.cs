using Agency.Huddle.App;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Wake;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// A review is a Task (#669, D-9, D-10): the assignee of a Task tagged <c>review</c> or <c>verify</c> is woken like any assignee, and the
/// wake ends with the review sentence. No wake rule is named after a challenger any more.
/// </summary>
public sealed class ReviewTaskWakeTests
{
    private const string ReviewSentence = "Review the work, then record the outcome on this Task: approve by moving it to Done, or leave it open with your findings as a comment; a reply in chat alone does not move it. A comment wakes you only when it Mentions you: when the owner comments that the work is ready again and Mentions you, review it again on this Task.";

    /// <summary>Creating a Task tagged review or verify, assigned to a reviewer, wakes the reviewer, and the wake text ends with the review sentence.</summary>
    /// <param name="tag">The tag.</param>
    [Theory]
    [InlineData("review")]
    [InlineData("verify")]
    [InlineData("Review")]
    public async Task Create_AssignsReviewer_WakesReviewerWithReviewSentence(string tag)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id, parent: new TaskId("PLAT", 1), tags: [tag]));

        await RaiseAndFireAsync(harness, task, cast.KaiActor, "created");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.EndsWith(ReviewSentence, post.Message.Text, StringComparison.Ordinal); // contains-ok: pins the wording a Human or an Agent reads
        Assert.Equal(WakeOutcome.Woken, harness.Activity.LastWake(task.Id)?.Outcome);
    }

    /// <summary>A Task without the review or verify tag gets the wake but not the review sentence.</summary>
    [Fact]
    public async Task Create_UntaggedTask_WakeCarriesNoReviewSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id, tags: ["frontend"]));

        await RaiseAndFireAsync(harness, task, cast.KaiActor, "created");

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.DoesNotContain("Review the work, then record the outcome", post.Message.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A "ready again" comment from the owner that Mentions the reviewer wakes the assignee of a review Task, and the wake ends with the review sentence,
    /// which tells the reviewer a Mentioning comment is what wakes them again (smart wakes, correction 11). The sentence is pinned whole, as the last line.
    /// </summary>
    [Fact]
    public async Task Comment_MentioningTheReviewer_WakesWithTheReviewSentenceAsItsLastLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id, parent: new TaskId("PLAT", 1), tags: ["review"]));

        harness.Events.RaiseTaskChanged(new TaskChange(
            task,
            task,
            [],
            cast.KaiActor,
            new ChangeLogEntry(harness.Clock.GetUtcNow(), "Kai", "comment added by Kai"),
            Kind: TaskChangeKind.Comment,
            Comment: "Ready for another look, @Nova."));
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(ReviewSentence, post.Message.Text.TrimEnd().Split('\n')[^1]);
    }

    /// <summary>A comment on a review Task that Mentions no one wakes no one, the reviewer included: it is the Mention, not the comment, that wakes.</summary>
    [Fact]
    public async Task Comment_WithoutAMention_DoesNotWakeTheReviewer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id, parent: new TaskId("PLAT", 1), tags: ["review"]));

        harness.Events.RaiseTaskChanged(new TaskChange(
            task,
            task,
            [],
            cast.KaiActor,
            new ChangeLogEntry(harness.Clock.GetUtcNow(), "Kai", "comment added by Kai"),
            Kind: TaskChangeKind.Comment,
            Comment: "Ready for another look."));
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
    }

    /// <summary>No registered wake rule is a challenger rule: the production names hold none.</summary>
    [Fact]
    public void ProductionOrder_NamesNoChallengerRule()
    {
        Assert.DoesNotContain(TaskWakeRules.ProductionOrder, static name => name.Contains("Challenger", StringComparison.OrdinalIgnoreCase));
    }
}
