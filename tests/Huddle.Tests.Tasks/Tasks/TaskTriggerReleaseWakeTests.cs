using Agency.Huddle.App;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>The wake <see cref="TaskTriggerService"/> posts for a release (ADR-0088): the assignee is told the merge needs no Accept and a push still does.</summary>
public sealed class TaskTriggerReleaseWakeTests
{
    private const string ReleaseSummary = "proceeded under project level Run inside the Team: its merge into main needs no Accept";

    /// <summary>A release by Huddle wakes the assignee, and the wake carries the merge instruction and the push caveat with the scope word.</summary>
    [Fact]
    public async Task Release_WakesTheAssigneeWithTheMergeInstruction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, TaskActors.Huddle, ReleaseSummary);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Contains(
            "Task PLAT-0001's Proof is complete and its project level is Run inside the Team, so its merge needs no Accept. Merge its work into the local main now, then move the Task to Done. A push still waits for the Human's Accept: put a push in the Task's Action.",
            post.Message.Text,
            StringComparison.Ordinal);
        Assert.Equal(WakeOutcome.Woken, harness.Activity.LastWake(task.Id)?.Outcome);
    }
}
