using Agency.Huddle.App;
using Agency.Huddle.App.Tasks;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskActivity"/>, pinning Spec §10.7 and the budget arithmetic of Spec §10.6.</summary>
public sealed class TaskActivityTests
{
    private static TaskActivity Make(int agentWakeBudget = 10)
    {
        TeamOptions options = new();
        options.Tasks.AgentWakeBudget = agentWakeBudget;
        return new TaskActivity(Options.Create(options));
    }

    /// <summary>Recording a wake sets LastWake and raises Woken with the recorded value.</summary>
    [Fact]
    public void Record_SetsLastWake_RaisesWoken()
    {
        TaskActivity activity = Make();
        TaskId id = new("PLAT", 1);
        WakeRecord? raised = null;
        activity.Woken += r => raised = r;
        WakeRecord record = new(id, "Nova", "room-1", "General", WakeOutcome.Woken, DateTimeOffset.UnixEpoch);

        activity.Record(record);

        Assert.Equal(record, activity.LastWake(id));
        Assert.Equal(record, raised);
    }

    /// <summary>CountAgentWake increments the Used count of the Task's budget.</summary>
    [Fact]
    public void CountAgentWake_IncrementsUsed()
    {
        TaskActivity activity = Make();
        TaskId id = new("PLAT", 2);

        activity.CountAgentWake(id);
        activity.CountAgentWake(id);

        Assert.Equal(2, activity.Budget(id).Used);
    }

    /// <summary>A Task's budget is Exhausted once Used reaches Granted (default AgentWakeBudget).</summary>
    [Fact]
    public void Budget_ExhaustedAtGranted()
    {
        TaskActivity activity = Make(agentWakeBudget: 2);
        TaskId id = new("PLAT", 3);

        activity.CountAgentWake(id);
        Assert.False(activity.Budget(id).Exhausted);

        activity.CountAgentWake(id);
        Assert.True(activity.Budget(id).Exhausted);
    }

    /// <summary>Grant on an exhausted budget adds another AgentWakeBudget to Granted.</summary>
    [Fact]
    public void Grant_AddsAnotherBudget()
    {
        TaskActivity activity = Make(agentWakeBudget: 2);
        TaskId id = new("PLAT", 4);
        activity.CountAgentWake(id);
        activity.CountAgentWake(id);
        Assert.True(activity.Budget(id).Exhausted);

        activity.Grant(id);

        WakeBudget budget = activity.Budget(id);
        Assert.Equal(4, budget.Granted);
        Assert.False(budget.Exhausted);
    }

    /// <summary>ResetForHuman zeroes the Used count of the Task's budget.</summary>
    [Fact]
    public void ResetForHuman_ZeroesUsed()
    {
        TaskActivity activity = Make();
        TaskId id = new("PLAT", 5);
        activity.CountAgentWake(id);

        activity.ResetForHuman(id);

        Assert.Equal(0, activity.Budget(id).Used);
    }

    /// <summary>A Task with AgentWakeBudget of zero or less is never Exhausted, however many wakes are counted.</summary>
    [Fact]
    public void Budget_ZeroOrLess_NeverExhausted()
    {
        TaskActivity activity = Make(agentWakeBudget: 0);
        TaskId id = new("PLAT", 6);

        activity.CountAgentWake(id);
        activity.CountAgentWake(id);

        Assert.False(activity.Budget(id).Exhausted);
    }

    /// <summary>ResetForHuman resets Granted back to the configured base as well as Used (corrections-B3 D8).</summary>
    [Fact]
    public void ResetForHuman_ZeroesUsedAndGrants()
    {
        TaskActivity activity = Make(agentWakeBudget: 2);
        TaskId id = new("PLAT", 7);
        activity.CountAgentWake(id);
        activity.CountAgentWake(id);
        activity.Grant(id);
        Assert.Equal(4, activity.Budget(id).Granted);

        activity.ResetForHuman(id);

        WakeBudget budget = activity.Budget(id);
        Assert.Equal(0, budget.Used);
        Assert.Equal(2, budget.Granted);
    }

    /// <summary>Grant on a budget that is not yet Exhausted makes no change (corrections-B3 D8).</summary>
    [Fact]
    public void Grant_NotExhausted_NoChange()
    {
        TaskActivity activity = Make(agentWakeBudget: 2);
        TaskId id = new("PLAT", 8);
        activity.CountAgentWake(id);
        WakeBudget before = activity.Budget(id);

        activity.Grant(id);

        Assert.Equal(before, activity.Budget(id));
    }

    /// <summary>TryConsumeAgentWake succeeds and increments Used while the budget is not exhausted.</summary>
    [Fact]
    public void TryConsumeAgentWake_UnderBudget_TrueAndIncrementsUsed()
    {
        TaskActivity activity = Make(agentWakeBudget: 2);
        TaskId id = new("PLAT", 9);

        bool consumed = activity.TryConsumeAgentWake(id);

        Assert.True(consumed);
        Assert.Equal(1, activity.Budget(id).Used);
    }

    /// <summary>TryConsumeAgentWake fails and leaves the budget unchanged once it is already exhausted.</summary>
    [Fact]
    public void TryConsumeAgentWake_AtBudget_FalseAndUnchanged()
    {
        TaskActivity activity = Make(agentWakeBudget: 1);
        TaskId id = new("PLAT", 10);
        Assert.True(activity.TryConsumeAgentWake(id));
        WakeBudget before = activity.Budget(id);

        bool consumed = activity.TryConsumeAgentWake(id);

        Assert.False(consumed);
        Assert.Equal(before, activity.Budget(id));
    }

    /// <summary>A Task with AgentWakeBudget of zero or less always succeeds (matches Budget_ZeroOrLess_NeverExhausted).</summary>
    [Fact]
    public void TryConsumeAgentWake_ZeroOrLessBudget_AlwaysTrue()
    {
        TaskActivity activity = Make(agentWakeBudget: 0);
        TaskId id = new("PLAT", 11);

        Assert.True(activity.TryConsumeAgentWake(id));
        Assert.True(activity.TryConsumeAgentWake(id));
    }

    /// <summary>
    /// Under concurrent callers, TryConsumeAgentWake never lets Used exceed Granted. Uses real OS
    /// threads released together from one gate, rather than <c>Task.Run</c>, so an unsynchronised
    /// check-then-act genuinely overlaps instead of being serialised by thread-pool warm-up, and
    /// repeats the race across many rounds and Task ids: a lost lock is a timing-dependent race, so
    /// a single round can pass by chance even when the guard is gone.
    /// </summary>
    [Fact]
    public void TryConsumeAgentWake_Concurrent_NeverExceedsGranted()
    {
        const int callerCount = 64;
        const int rounds = 25;
        TaskActivity activity = Make(agentWakeBudget: 10);

        for (int round = 0; round < rounds; round++)
        {
            TaskId id = new("PLAT", 100 + round);
            using ManualResetEventSlim gate = new(initialState: false);
            bool[] results = new bool[callerCount];
            Thread[] threads = new Thread[callerCount];
            for (int i = 0; i < callerCount; i++)
            {
                int index = i;
                threads[i] = new Thread(() =>
                {
                    gate.Wait();
                    results[index] = activity.TryConsumeAgentWake(id);
                });
                threads[i].Start();
            }

            gate.Set();
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            Assert.Equal(10, results.Count(consumed => consumed));
            Assert.Equal(10, activity.Budget(id).Used);
        }
    }

    /// <summary>RefundAgentWake decrements Used, and never takes it below zero.</summary>
    [Fact]
    public void RefundAgentWake_DecrementsUsed_NeverBelowZero()
    {
        TaskActivity activity = Make(agentWakeBudget: 2);
        TaskId id = new("PLAT", 13);
        activity.CountAgentWake(id);

        activity.RefundAgentWake(id);
        Assert.Equal(0, activity.Budget(id).Used);

        activity.RefundAgentWake(id);
        Assert.Equal(0, activity.Budget(id).Used);
    }

    /// <summary>Grant raises Changed when it actually adds another budget (corrections-B5).</summary>
    [Fact]
    public void Grant_RaisesChanged()
    {
        TaskActivity activity = Make(agentWakeBudget: 1);
        TaskId id = new("PLAT", 14);
        activity.CountAgentWake(id);
        int changedCount = 0;
        activity.Changed += () => changedCount++;

        activity.Grant(id);

        Assert.Equal(1, changedCount);
    }

    /// <summary>ResetForHuman raises Changed (corrections-B5).</summary>
    [Fact]
    public void ResetForHuman_RaisesChanged()
    {
        TaskActivity activity = Make();
        TaskId id = new("PLAT", 15);
        int changedCount = 0;
        activity.Changed += () => changedCount++;

        activity.ResetForHuman(id);

        Assert.Equal(1, changedCount);
    }
}
