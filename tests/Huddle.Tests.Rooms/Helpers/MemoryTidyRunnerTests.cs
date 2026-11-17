using Agency.Huddle.App.Helpers.MemoryTidy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="MemoryTidyRunner"/> owns every Memory Tidy pass (Memory Tidy spec §6.7, §8.1): one at a time on its own task, recovery before the first
/// request, <c>RunAtStart</c>, a pass that throws, and <c>Changed</c>. Every wait is a task the test completes; no test waits real time.
/// </summary>
public sealed class MemoryTidyRunnerTests
{
    /// <summary>A request while a pass runs is answered <see langword="null"/> and starts nothing; the pass that runs is not the caller's and ends as it was told to.</summary>
    [Fact]
    public async Task TryStart_WhilePassRuns_AnswersNullAndStartsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource entered = Gate();
        TaskCompletionSource release = Gate();
        FakePass pass = new(async (_, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return MemoryTidyPassEnd.Done;
        });
        using MemoryTidyRunner runner = Runner(pass, new FakeRecovery());
        await runner.StartAsync(ct);

        Task<MemoryTidyPassEnd>? first = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(first);
        await entered.Task.WaitAsync(ct);
        Task<MemoryTidyPassEnd>? second = runner.TryStart(MemoryTidyStart.Timetable);
        bool runningWhileBlocked = runner.IsRunning;
        bool firstDoneWhileBlocked = first.IsCompleted;
        release.SetResult();
        MemoryTidyPassEnd end = await first.WaitAsync(ct);

        Assert.Null(second);
        Assert.True(runningWhileBlocked);
        Assert.False(firstDoneWhileBlocked);
        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal([MemoryTidyStart.TidyNow], pass.Starts);
        Assert.False(runner.IsRunning);
        await runner.StopAsync(ct);
    }

    /// <summary>A stop reaches the running pass through the runner's own token, the pass ends as <c>Stopped</c>, and the host's stop waits for it.</summary>
    [Fact]
    public async Task StopAsync_WhilePassRuns_EndsItAsStoppedAndWaitsForIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource entered = Gate();
        FakePass pass = new(async (_, token) =>
        {
            entered.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException)
            {
                // The stop is what this pass waits for.
            }

            return MemoryTidyPassEnd.Stopped;
        });
        using MemoryTidyRunner runner = Runner(pass, new FakeRecovery());
        await runner.StartAsync(ct);
        Task<MemoryTidyPassEnd>? running = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(running);
        await entered.Task.WaitAsync(ct);

        await runner.StopAsync(ct);

        Assert.True(running.IsCompleted);
        Assert.Equal(MemoryTidyPassEnd.Stopped, await running);
        Assert.Null(runner.TryStart(MemoryTidyStart.TidyNow));
    }

    /// <summary>Recovery finishes before the first request is taken: the pass starts only after recovery has ended.</summary>
    [Fact]
    public async Task ExecuteAsync_RecoveryRunsBeforeTheFirstRequest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> order = [];
        TaskCompletionSource recoveryGate = Gate();
        FakeRecovery recovery = new(async token =>
        {
            await recoveryGate.Task.WaitAsync(token);
            lock (order)
            {
                order.Add("recovered");
            }
        });
        FakePass pass = new((_, _) =>
        {
            lock (order)
            {
                order.Add("pass");
            }

            return Task.FromResult(MemoryTidyPassEnd.Done);
        });
        using MemoryTidyRunner runner = Runner(pass, recovery);
        await runner.StartAsync(ct);
        Task<MemoryTidyPassEnd>? first = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(first);

        recoveryGate.SetResult();
        _ = await first.WaitAsync(ct);

        Assert.Equal(["recovered", "pass"], order);
        await runner.StopAsync(ct);
    }

    /// <summary><c>RunAtStart</c> true starts one pass after the thirty-second delay, as <c>RunAtStart</c>.</summary>
    [Fact]
    public async Task ExecuteAsync_RunAtStartTrue_StartsOnePassAfterThirtySeconds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource delay = Gate();
        TaskCompletionSource ran = Gate();
        TimeSpan asked = TimeSpan.Zero;
        FakePass pass = new((_, _) =>
        {
            ran.SetResult();
            return Task.FromResult(MemoryTidyPassEnd.Done);
        });
        using MemoryTidyRunner runner = Runner(pass, new FakeRecovery(), runAtStart: true);
        runner.DelayForTests = (span, token) =>
        {
            asked = span;
            return delay.Task.WaitAsync(token);
        };
        await runner.StartAsync(ct);

        delay.SetResult();
        await ran.Task.WaitAsync(ct);
        await runner.StopAsync(ct);

        Assert.Equal(TimeSpan.FromSeconds(30), asked);
        Assert.Equal([MemoryTidyStart.RunAtStart], pass.Starts);
    }

    /// <summary><c>RunAtStart</c> false never waits and never starts a pass of its own: the only pass is the one asked for.</summary>
    [Fact]
    public async Task ExecuteAsync_RunAtStartFalse_StartsNoPassOfItsOwn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        bool delayAsked = false;
        FakePass pass = new((_, _) => Task.FromResult(MemoryTidyPassEnd.Done));
        using MemoryTidyRunner runner = Runner(pass, new FakeRecovery(), runAtStart: false);
        runner.DelayForTests = (_, _) =>
        {
            delayAsked = true;
            return Task.CompletedTask;
        };
        await runner.StartAsync(ct);

        Task<MemoryTidyPassEnd>? asked = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(asked);
        _ = await asked.WaitAsync(ct);
        await runner.StopAsync(ct);

        Assert.False(delayAsked);
        Assert.Equal([MemoryTidyStart.TidyNow], pass.Starts);
    }

    /// <summary>A pass that throws is logged, ends as <c>Threw</c>, its list is finished (recovery is asked again), and the next request is taken.</summary>
    [Fact]
    public async Task TryStart_PassThrows_EndsAsThrewFinishesItsListAndTakesTheNextRequest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int calls = 0;
        FakePass pass = new((_, _) => ++calls == 1
            ? throw new InvalidOperationException("The pass failed.")
            : Task.FromResult(MemoryTidyPassEnd.Done));
        FakeRecovery recovery = new();
        using MemoryTidyRunner runner = Runner(pass, recovery);
        await runner.StartAsync(ct);

        Task<MemoryTidyPassEnd>? first = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(first);
        MemoryTidyPassEnd firstEnd = await first.WaitAsync(ct);
        Task<MemoryTidyPassEnd>? second = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(second);
        MemoryTidyPassEnd secondEnd = await second.WaitAsync(ct);
        await runner.StopAsync(ct);

        Assert.Equal(MemoryTidyPassEnd.Threw, firstEnd);
        Assert.Equal(MemoryTidyPassEnd.Done, secondEnd);
        Assert.Equal(2, recovery.Calls);
    }

    /// <summary><c>Changed</c> is raised when a pass starts and when it ends, and a subscriber that throws does not stop either.</summary>
    [Fact]
    public async Task Changed_IsRaisedAtStartAndAtEnd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int raised = 0;
        TaskCompletionSource entered = Gate();
        TaskCompletionSource release = Gate();
        int raisedWhileRunning = -1;
        FakePass pass = new(async (_, token) =>
        {
            raisedWhileRunning = Volatile.Read(ref raised);
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return MemoryTidyPassEnd.Done;
        });
        using MemoryTidyRunner runner = Runner(pass, new FakeRecovery());
        runner.Changed += () => throw new InvalidOperationException("A subscriber threw.");
        runner.Changed += () => Interlocked.Increment(ref raised);
        await runner.StartAsync(ct);

        Task<MemoryTidyPassEnd>? running = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(running);
        await entered.Task.WaitAsync(ct);
        release.SetResult();
        _ = await running.WaitAsync(ct);
        await runner.StopAsync(ct);

        Assert.Equal(1, raisedWhileRunning);
        Assert.Equal(2, Volatile.Read(ref raised));
    }

    /// <summary>A pass that lets the cancellation escape as an exception is still a stop: the runner reports <c>Stopped</c>, not <c>Threw</c>, and does not ask recovery again.</summary>
    [Fact]
    public async Task StopAsync_PassLetsCancellationEscape_EndsAsStoppedNotThrew()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource entered = Gate();
        FakePass pass = new(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return MemoryTidyPassEnd.Done;
        });
        FakeRecovery recovery = new();
        using MemoryTidyRunner runner = Runner(pass, recovery);
        await runner.StartAsync(ct);
        Task<MemoryTidyPassEnd>? running = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(running);
        await entered.Task.WaitAsync(ct);

        await runner.StopAsync(ct);

        Assert.Equal(MemoryTidyPassEnd.Stopped, await running);
        Assert.Equal(1, recovery.Calls);
    }

    /// <summary>A <c>Changed</c> subscriber that throws neither skips the subscriber after it nor fails the pass.</summary>
    [Fact]
    public async Task Changed_SubscriberThrows_OtherSubscribersAndThePassAreUnaffected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int raised = 0;
        using MemoryTidyRunner runner = Runner(new FakePass((_, _) => Task.FromResult(MemoryTidyPassEnd.Done)), new FakeRecovery());
        runner.Changed += () => throw new InvalidOperationException("A subscriber threw.");
        runner.Changed += () => Interlocked.Increment(ref raised);
        await runner.StartAsync(ct);

        Task<MemoryTidyPassEnd>? running = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(running);
        MemoryTidyPassEnd end = await running.WaitAsync(ct);
        await runner.StopAsync(ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal(2, Volatile.Read(ref raised));
    }

    /// <summary>A request still queued when the host stops is answered <c>Stopped</c> and its pass never runs.</summary>
    [Fact]
    public async Task StopAsync_RequestStillQueued_IsAnsweredStopped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource never = Gate();
        FakePass pass = new((_, _) => Task.FromResult(MemoryTidyPassEnd.Done));
        FakeRecovery recovery = new(token => never.Task.WaitAsync(token));
        using MemoryTidyRunner runner = Runner(pass, recovery);
        await runner.StartAsync(ct);
        Task<MemoryTidyPassEnd>? queued = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(queued);

        await runner.StopAsync(ct);

        Assert.Equal(MemoryTidyPassEnd.Stopped, await queued);
        Assert.Empty(pass.Starts);
        Assert.False(runner.IsRunning);
    }

    /// <summary>A host that stops during the thirty-second wait of <c>RunAtStart</c> starts no pass.</summary>
    [Fact]
    public async Task StopAsync_DuringTheRunAtStartDelay_StartsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakePass pass = new((_, _) => Task.FromResult(MemoryTidyPassEnd.Done));
        using MemoryTidyRunner runner = Runner(pass, new FakeRecovery(), runAtStart: true);
        runner.DelayForTests = static (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token);
        await runner.StartAsync(ct);

        await runner.StopAsync(ct);

        Assert.Empty(pass.Starts);
    }

    /// <summary>Recovery that throws at start-up is logged and the runner still serves the next request.</summary>
    [Fact]
    public async Task ExecuteAsync_RecoveryThrows_StillServesTheNextRequest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakePass pass = new((_, _) => Task.FromResult(MemoryTidyPassEnd.Done));
        FakeRecovery recovery = new(static _ => throw new InvalidOperationException("Recovery failed."));
        using MemoryTidyRunner runner = Runner(pass, recovery);
        await runner.StartAsync(ct);

        Task<MemoryTidyPassEnd>? running = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(running);
        MemoryTidyPassEnd end = await running.WaitAsync(ct);
        await runner.StopAsync(ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal(1, recovery.Calls);
    }

    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static MemoryTidyRunner Runner(FakePass pass, FakeRecovery recovery, bool runAtStart = false) =>
        new(pass, recovery, Options.Create(new Agency.Huddle.App.TeamOptions { Memory = { Tidy = { RunAtStart = runAtStart } } }), NullLogger<MemoryTidyRunner>.Instance);

    private sealed class FakePass(Func<MemoryTidyStart, CancellationToken, Task<MemoryTidyPassEnd>> run) : IMemoryTidyPass
    {
        private readonly List<MemoryTidyStart> starts = [];

        public IReadOnlyList<MemoryTidyStart> Starts
        {
            get
            {
                lock (this.starts)
                {
                    return [.. this.starts];
                }
            }
        }

        public Task<MemoryTidyPassEnd> RunAsync(MemoryTidyStart start, CancellationToken cancellationToken)
        {
            lock (this.starts)
            {
                this.starts.Add(start);
            }

            return run(start, cancellationToken);
        }
    }

    private sealed class FakeRecovery(Func<CancellationToken, Task>? run = null) : IMemoryTidyRecovery
    {
        private int calls;

        public int Calls => Volatile.Read(ref this.calls);

        public Task RecoverAsync(CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref this.calls);
            return run?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }
    }
}
