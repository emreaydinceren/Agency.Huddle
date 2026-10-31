using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Watching;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Watching;

/// <summary>Tests for <see cref="DebouncedFileWatcher"/>: a burst settles once, Dispose silences it, and an Error reschedules a settle.</summary>
public sealed class DebouncedFileWatcherTests
{
    /// <summary>How long a test waits for a callback that should arrive before it fails.</summary>
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>A pause comfortably longer than the debounce, to prove no further callback follows.</summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(DebouncedFileWatcher.DebounceMilliseconds * 2);

    /// <summary>
    /// A burst of writes spanning more than one debounce window, each closer to the last than the window,
    /// produces exactly one callback, and that callback arrives only after the burst has ended.
    /// </summary>
    [Fact]
    public async Task Burst_OfWrites_ProducesOneSettledCallbackAfterTheBurstEnds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string file = Path.Combine(dir.Path, "watched.json");
        Probe probe = new();
        using DebouncedFileWatcher watcher = new(dir.Path, "watched.json", probe.OnSettled, new RecordingLogger<DebouncedFileWatcherTests>(), "Test");

        // A second, raw watcher sees when the operating system actually delivered each event. The writer's own gaps cannot show a
        // delivery stall on a loaded machine (events arriving late and bunched), and that stall is what splits a burst in two.
        ArrivalLog arrivals = new();
        using FileSystemWatcher observer = new(dir.Path, "watched.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
        observer.Changed += arrivals.OnEvent;
        observer.Created += arrivals.OnEvent;
        observer.EnableRaisingEvents = true;

        Stopwatch sinceLastWrite = new();
        TimeSpan longestGap = TimeSpan.Zero;
        for (int i = 0; i < 10; i++)
        {
            if (sinceLastWrite.IsRunning && sinceLastWrite.Elapsed > longestGap)
            {
                longestGap = sinceLastWrite.Elapsed;
            }

            File.WriteAllText(file, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sinceLastWrite.Restart();
            await Task.Delay(100, ct);
        }

        probe.MarkBurstOver();

        // A machine stalled long enough to open a gap of a whole debounce window between two writes makes
        // a second callback correct, not a bug; the result proves nothing either way, so skip, don't fail.
        Assert.SkipWhen(
            longestGap >= TimeSpan.FromMilliseconds(DebouncedFileWatcher.DebounceMilliseconds)
            || arrivals.LongestGap >= TimeSpan.FromMilliseconds(DebouncedFileWatcher.DebounceMilliseconds),
            "The machine stalled between writes, or between the operating system's events, for a whole debounce window, so the burst was not one burst.");
        await probe.FirstCall.WaitAsync(WaitTimeout, ct);
        await Task.Delay(QuietPeriod, ct);

        Assert.Equal(1, probe.Calls);
        Assert.False(probe.FiredBeforeBurstEnded);
    }

    /// <summary>
    /// Disposing the watcher right after a write means its callback never fires, while a second, live
    /// watcher on the same file does - so the silence is the Dispose, not an absent event. The claim only
    /// holds when the Dispose lands inside the debounce window: a callback that settled before it is
    /// correct behaviour. So the window from the write to the end of Dispose is timed, and an attempt the
    /// machine stalled past the window (a 100 ms delay resumed ~400 ms late on a starved CI runner) is
    /// re-run rather than asserted on.
    /// </summary>
    [Fact]
    public async Task Dispose_AfterAWrite_NeverCallsBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TimeSpan window = TimeSpan.FromMilliseconds(DebouncedFileWatcher.DebounceMilliseconds);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            (TimeSpan elapsed, int disposedCalls, int controlCalls) = await DisposeAfterAWriteAsync(ct);
            if (elapsed < window)
            {
                Assert.Equal(0, disposedCalls);
                Assert.Equal(1, controlCalls);
                return;
            }
        }

        Assert.Fail($"No attempt disposed the watcher within {window.TotalMilliseconds} ms of its write, so the race was never arranged.");
    }

    /// <summary>
    /// One attempt of <see cref="Dispose_AfterAWrite_NeverCallsBack"/>: writes the file, disposes the watcher
    /// (twice, for idempotence) about 100 ms later, then waits for the control watcher to settle and a quiet period.
    /// </summary>
    /// <param name="ct">The test's token.</param>
    /// <returns>The time from just before the write to just after the Dispose, and each watcher's callback count.</returns>
    private static async Task<(TimeSpan Elapsed, int DisposedCalls, int ControlCalls)> DisposeAfterAWriteAsync(CancellationToken ct)
    {
        using TempDataDir dir = new();
        string file = Path.Combine(dir.Path, "watched.json");
        Probe disposed = new();
        Probe control = new();
        RecordingLogger<DebouncedFileWatcherTests> logger = new();
        DebouncedFileWatcher watcher = new(dir.Path, "watched.json", disposed.OnSettled, logger, "Test");
        using DebouncedFileWatcher controlWatcher = new(dir.Path, "watched.json", control.OnSettled, logger, "Control");

        // Started before the write: the watcher can see the file's creation before WriteAllText returns.
        long start = Stopwatch.GetTimestamp();
        File.WriteAllText(file, "x");
        await Task.Delay(100, ct);
        watcher.Dispose();
        watcher.Dispose();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

        await control.FirstCall.WaitAsync(WaitTimeout, ct);
        await Task.Delay(QuietPeriod, ct);
        return (elapsed, disposed.Calls, control.Calls);
    }

    /// <summary>
    /// A watcher Error (the signal that the kernel buffer overflowed and events were dropped) logs a warning
    /// and schedules a settle even though no file event arrived.
    /// </summary>
    [Fact]
    public async Task WatcherError_SchedulesASettle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Probe probe = new();
        RecordingLogger<DebouncedFileWatcherTests> logger = new();
        using DebouncedFileWatcher watcher = new(dir.Path, "watched.json", probe.OnSettled, logger, "TestStore");

        await Task.Delay(QuietPeriod, ct);
        Assert.Equal(0, probe.Calls);

        watcher.OnWatcherError(this, new ErrorEventArgs(new InternalBufferOverflowException()));
        await probe.FirstCall.WaitAsync(WaitTimeout, ct);

        Assert.Equal(1, probe.Calls);
        Assert.Contains(
            logger.Snapshot(),
            static entry => entry.Level == LogLevel.Warning && entry.Message.Contains("TestStore's FileSystemWatcher reported an error", StringComparison.Ordinal));
    }

    /// <summary>A watcher Error after Dispose neither logs nor schedules anything, so teardown cannot crash on a stale logger.</summary>
    [Fact]
    public async Task WatcherError_AfterDispose_DoesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Probe probe = new();
        RecordingLogger<DebouncedFileWatcherTests> logger = new();
        DebouncedFileWatcher watcher = new(dir.Path, "watched.json", probe.OnSettled, logger, "TestStore");
        watcher.Dispose();

        watcher.OnWatcherError(this, new ErrorEventArgs(new InternalBufferOverflowException()));
        await Task.Delay(QuietPeriod, ct);

        Assert.Equal(0, probe.Calls);
        Assert.Empty(logger.Snapshot());
    }

    /// <summary>Records when each raw watcher event arrived, to find the longest silence between two of them.</summary>
    private sealed class ArrivalLog
    {
        private readonly Lock gate = new();
        private long lastTimestamp;
        private TimeSpan longestGap = TimeSpan.Zero;

        /// <summary>The longest time between two consecutive events.</summary>
        public TimeSpan LongestGap
        {
            get
            {
                lock (this.gate)
                {
                    return this.longestGap;
                }
            }
        }

        /// <summary>The raw watcher's handler.</summary>
        /// <param name="sender">Unused.</param>
        /// <param name="e">Unused.</param>
        public void OnEvent(object sender, FileSystemEventArgs e)
        {
            long now = Stopwatch.GetTimestamp();
            lock (this.gate)
            {
                if (this.lastTimestamp != 0)
                {
                    TimeSpan gap = Stopwatch.GetElapsedTime(this.lastTimestamp, now);
                    if (gap > this.longestGap)
                    {
                        this.longestGap = gap;
                    }
                }

                this.lastTimestamp = now;
            }
        }
    }

    /// <summary>The callback a test hands the watcher: counts calls and records whether any came before the burst ended.</summary>
    private sealed class Probe
    {
        private readonly TaskCompletionSource firstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        private volatile bool burstOver;
        private volatile bool firedBeforeBurstEnded;

        /// <summary>Completes when the first callback arrives.</summary>
        public Task FirstCall => this.firstCall.Task;

        /// <summary>How many callbacks have arrived.</summary>
        public int Calls => Volatile.Read(ref this.calls);

        /// <summary>Whether any callback arrived before <see cref="MarkBurstOver"/>.</summary>
        public bool FiredBeforeBurstEnded => this.firedBeforeBurstEnded;

        /// <summary>The watcher's callback.</summary>
        public void OnSettled()
        {
            if (!this.burstOver)
            {
                this.firedBeforeBurstEnded = true;
            }

            _ = Interlocked.Increment(ref this.calls);
            _ = this.firstCall.TrySetResult();
        }

        /// <summary>Records that the last write of the burst has been made.</summary>
        public void MarkBurstOver() => this.burstOver = true;
    }
}
