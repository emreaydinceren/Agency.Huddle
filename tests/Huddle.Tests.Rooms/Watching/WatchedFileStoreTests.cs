using Agency.Huddle.App.Watching;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Watching;

/// <summary>Tests for <see cref="WatchedFileStore{TSnapshot}"/>: publish-then-raise outside the gate, keep-on-null, suppress-on-null, and Dispose.</summary>
public sealed class WatchedFileStoreTests
{
    /// <summary>How long a test waits for a callback that should arrive before it fails.</summary>
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>A pause comfortably longer than the debounce, to prove nothing further follows.</summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(DebouncedFileWatcher.DebounceMilliseconds * 3);

    /// <summary>
    /// An outside write rebuilds the snapshot, publishes it, and the captured action runs after the gate is released and
    /// already sees the new snapshot.
    /// </summary>
    [Fact]
    public async Task Settle_AfterOutsideWrite_PublishesThenRaisesOutsideTheGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string file = Path.Combine(dir.Path, "watched.txt");
        TaskCompletionSource<(string Seen, bool GateHeld)> raised = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WatchedFileStore<string>? holder = null;
        using WatchedFileStore<string> store = new(
            dir.Path,
            "watched.txt",
            "initial",
            _ => File.ReadAllText(file),
            (_, _) => () => raised.TrySetResult((holder?.Current ?? string.Empty, holder?.Gate.IsHeldByCurrentThread ?? true)),
            new RecordingLogger<WatchedFileStoreTests>(),
            "Test");
        holder = store;

        await File.WriteAllTextAsync(file, "changed", ct);
        (string seen, bool gateHeld) = await raised.Task.WaitAsync(WaitTimeout, ct);

        Assert.Equal("changed", seen);
        Assert.False(gateHeld);
    }

    /// <summary>A rebuild that returns <see langword="null"/> keeps the previous snapshot and raises nothing.</summary>
    [Fact]
    public async Task Settle_WhenRebuildReturnsNull_KeepsSnapshotAndRaisesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string file = Path.Combine(dir.Path, "watched.txt");
        TaskCompletionSource rebuilt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int raisedCount = 0;
        using WatchedFileStore<string> store = new(
            dir.Path,
            "watched.txt",
            "initial",
            _ =>
            {
                rebuilt.TrySetResult();
                return null;
            },
            (_, _) => () => Interlocked.Increment(ref raisedCount),
            new RecordingLogger<WatchedFileStoreTests>(),
            "Test");

        await File.WriteAllTextAsync(file, "changed", ct);
        await rebuilt.Task.WaitAsync(WaitTimeout, ct);
        await Task.Delay(QuietPeriod, ct);

        Assert.Equal("initial", store.Current);
        Assert.Equal(0, Volatile.Read(ref raisedCount));
    }

    /// <summary>An <c>onRebuilt</c> that returns <see langword="null"/> still publishes the snapshot but raises nothing.</summary>
    [Fact]
    public async Task Settle_WhenOnRebuiltReturnsNull_PublishesButRaisesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string file = Path.Combine(dir.Path, "watched.txt");
        TaskCompletionSource<(string Previous, string Rebuilt)> decided = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using WatchedFileStore<string> store = new(
            dir.Path,
            "watched.txt",
            "initial",
            _ => File.ReadAllText(file),
            (previous, rebuilt) =>
            {
                decided.TrySetResult((previous, rebuilt));
                return null;
            },
            new RecordingLogger<WatchedFileStoreTests>(),
            "Test");

        await File.WriteAllTextAsync(file, "changed", ct);
        (string previous, string rebuilt) = await decided.Task.WaitAsync(WaitTimeout, ct);

        Assert.Equal("initial", previous);
        Assert.Equal("changed", rebuilt);
        Assert.Equal("changed", store.Current);
    }

    /// <summary>After <see cref="WatchedFileStore{TSnapshot}.Dispose"/> a write neither rebuilds nor raises, and a second Dispose is harmless.</summary>
    [Fact]
    public async Task Dispose_BeforeAWrite_NeverRebuilds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string file = Path.Combine(dir.Path, "watched.txt");
        int rebuilds = 0;
        WatchedFileStore<string> store = new(
            dir.Path,
            "watched.txt",
            "initial",
            _ =>
            {
                Interlocked.Increment(ref rebuilds);
                return "rebuilt";
            },
            (_, _) => null,
            new RecordingLogger<WatchedFileStoreTests>(),
            "Test");

        store.Dispose();
        store.Dispose();
        await File.WriteAllTextAsync(file, "changed", ct);
        await Task.Delay(QuietPeriod, ct);

        Assert.True(store.IsDisposed);
        Assert.Equal(0, Volatile.Read(ref rebuilds));
    }
}
