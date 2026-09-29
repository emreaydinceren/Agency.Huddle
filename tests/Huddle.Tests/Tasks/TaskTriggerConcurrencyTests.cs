using Agency.Huddle.App;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Fires are serialised behind one gate (corrections-B3 D9 item 9, facts R3). Alone in its own class
/// because it holds real threads open for 25 rounds; the shared harness lives in
/// <see cref="TaskTriggerTestSupport"/>.
/// </summary>
public sealed class TaskTriggerConcurrencyTests
{
    /// <summary>
    /// Corrections-B3 D9 item 9 (and facts R3): fires are serialised behind one gate. Each round, two
    /// real threads released together raise one change each to the same Task as two different actors,
    /// with <c>WakeCoalesceSeconds = 0</c> so both fire at once; <see cref="OverlapProbe"/> holds the
    /// first fire open inside the directory and records whether the second ever overlaps it.
    /// </summary>
    [Fact]
    public async Task Concurrent_TwoBatchesSameTask_SerialisedByGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 0;
        options.Tasks.AgentWakeBudget = 0;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        OverlapProbe probe = new();
        harness.Directory.BeforeGetRoomMembers = probe.EnterAsync;

        for (int round = 0; round < ConcurrencyRounds; round++)
        {
            probe.NextRound();
            using ManualResetEventSlim go = new(initialState: false);
            Thread human = new(() =>
            {
                go.Wait(ct);
                harness.Raise(task, HumanActor, "By the Human");
            });
            Thread agent = new(() =>
            {
                go.Wait(ct);
                harness.Raise(task, cast.KaiActor, "By Kai");
            });
            human.Start();
            agent.Start();
            go.Set();
            human.Join();
            agent.Join();
            await harness.Trigger.WhenIdleAsync();
        }

        Assert.Equal(ConcurrencyRounds * 2, harness.Posts.Count);
        Assert.Equal(1, probe.MaxConcurrent);
    }
}