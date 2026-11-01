namespace Agency.Huddle.App.Watching;

/// <summary>
/// The skeleton every store that mirrors a file (or folder) on disk repeats: one immutable snapshot behind
/// a <see langword="volatile"/> reference, one write <see cref="Gate"/>, a <see cref="DebouncedFileWatcher"/>
/// and the watcher callback that re-reads the file, publishes the new snapshot and raises the store's
/// change event outside the lock. A store owns one of these by composition and keeps everything specific
/// to it - how to parse, how to write, which events exist.
/// </summary>
/// <remarks>
/// <para>
/// <b>Writers share the gate.</b> A store's own write methods take <see cref="Gate"/> (not a lock of their
/// own) and assign <see cref="Current"/> under it, so writers and the watcher callback exclude each other.
/// A writer raises its own event after releasing the gate, exactly as the callback here does.
/// </para>
/// <para>
/// <b>The callback this class runs</b> is the contract documented on <see cref="DebouncedFileWatcher"/>:
/// under <see cref="Gate"/> it returns if disposed, calls <c>rebuild</c> (a <see langword="null"/> result
/// keeps the previous snapshot and raises nothing), publishes the result, asks <c>onRebuilt</c> for the
/// action to run (capturing the event delegate while still locked), and only after releasing the gate
/// invokes that action. A <c>null</c> action from <c>onRebuilt</c> suppresses the event, which is how a
/// store skips an unchanged re-read.
/// </para>
/// </remarks>
/// <typeparam name="TSnapshot">The store's immutable resolved state.</typeparam>
internal sealed class WatchedFileStore<TSnapshot> : IDisposable
    where TSnapshot : class
{
    private readonly Func<TSnapshot, TSnapshot?> rebuild;
    private readonly Func<TSnapshot, TSnapshot, Action?> onRebuilt;
    private readonly DebouncedFileWatcher watcher;
    private volatile TSnapshot current;
    private bool disposed;

    /// <summary>Starts watching <paramref name="directory"/> for <paramref name="filter"/> with <paramref name="initial"/> as the first snapshot.</summary>
    /// <param name="directory">The directory to watch; it must already exist.</param>
    /// <param name="filter">The file-name filter, for example <c>appearance.json</c> or <c>*</c>.</param>
    /// <param name="initial">The snapshot the store loaded at construction.</param>
    /// <param name="rebuild">Re-reads the file after a settled change, given the current snapshot; returns <see langword="null"/> to keep the current one (every read attempt failed). Runs under <see cref="Gate"/>.</param>
    /// <param name="onRebuilt">Given the previous and the newly published snapshot, returns the action to invoke once the gate is released (normally the captured event delegate), or <see langword="null"/> to raise nothing. Runs under <see cref="Gate"/>, so it must only capture, never invoke.</param>
    /// <param name="logger">Used by the watcher to warn when it reports an error.</param>
    /// <param name="ownerName">The owning store's name, for that warning.</param>
    /// <param name="includeSubdirectories">Whether events under subdirectories count too.</param>
    /// <param name="notifyFilter">Which kinds of change raise events; <see langword="null"/> watches last-write and file-name changes.</param>
    public WatchedFileStore(
        string directory,
        string filter,
        TSnapshot initial,
        Func<TSnapshot, TSnapshot?> rebuild,
        Func<TSnapshot, TSnapshot, Action?> onRebuilt,
        ILogger logger,
        string ownerName,
        bool includeSubdirectories = false,
        NotifyFilters? notifyFilter = null)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(rebuild);
        ArgumentNullException.ThrowIfNull(onRebuilt);

        this.current = initial;
        this.rebuild = rebuild;
        this.onRebuilt = onRebuilt;
        this.watcher = new DebouncedFileWatcher(directory, filter, this.OnWatcherSettled, logger, ownerName, includeSubdirectories, notifyFilter);
    }

    /// <summary>The lock the store's write methods take; the watcher callback takes the same one.</summary>
    public Lock Gate { get; } = new();

    /// <summary>The published snapshot. Readers take no lock; assign it only while holding <see cref="Gate"/>.</summary>
    public TSnapshot Current
    {
        get => this.current;
        set => this.current = value;
    }

    /// <summary>Whether <see cref="Dispose"/> has run. Read it while holding <see cref="Gate"/>, as a write method that must not act on a disposed store does.</summary>
    public bool IsDisposed => this.disposed;

    /// <summary>Stops watching and releases the debounce timer. Idempotent.</summary>
    public void Dispose()
    {
        lock (this.Gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.watcher.Dispose();
    }

    /// <summary>Runs once the watcher settles; see the class remarks.</summary>
    private void OnWatcherSettled()
    {
        Action? raise;
        lock (this.Gate)
        {
            if (this.disposed)
            {
                return;
            }

            TSnapshot previous = this.current;
            TSnapshot? rebuilt = this.rebuild(previous);
            if (rebuilt is null)
            {
                return;
            }

            this.current = rebuilt;
            raise = this.onRebuilt(previous, rebuilt);
        }

        raise?.Invoke();
    }
}
