using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;
using AskOutcome = Agency.Huddle.App.Tasks.AskOutcome;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>The Ask on a Proof item: <see cref="TaskTriggerService"/> posts the Human's text to the item's owner (Human gate plan P6).</summary>
public sealed class TaskTriggerAskTests
{
    /// <summary>Ask posts the exact text, from the Human, into the Task's origin Room where the owner is a Member, and names that Room.</summary>
    [Fact]
    public async Task AskAsync_KnownOwner_PostsTheExactTextAsTheHumanInTheChosenRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct, start: false);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Kai", originRoomId: cast.Room.Id));

        AskOutcome outcome = await harness.Trigger.AskAsync(task, "Nova", "@Nova where is the evidence for item 2?", ct);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("@Nova where is the evidence for item 2?", post.Message.Text);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
        Assert.Equal(cast.Room.Id, post.Room.Id);
        Assert.Contains(post.Members, member => member.Id == cast.Nova.Id);
        Assert.True(outcome.Succeeded);
        Assert.Equal("Platform", outcome.RoomName);
    }

    /// <summary>An owner who is not a known user fails the Ask and posts nothing.</summary>
    [Fact]
    public async Task AskAsync_UnknownOwner_FailsAndPostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct, start: false);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Kai", originRoomId: cast.Room.Id));

        AskOutcome outcome = await harness.Trigger.AskAsync(task, "Ghost", "@Ghost are you there?", ct);

        Assert.False(outcome.Succeeded);
        Assert.Empty(harness.Posts);
    }
}
