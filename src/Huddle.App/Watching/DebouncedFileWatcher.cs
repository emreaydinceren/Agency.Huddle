namespace Agency.Huddle.App.Watching;

/// <summary>
/// Watches a directory for changes and calls back once per settled burst of them, so a store that
/// mirrors a file on disk does not rebuild (and raise its change event) for every one of the several
/// events a single editor save produces. Owns the <see cref="FileSystemWatcher"/>, the debounce timer
/// and the lock and disposed flag that guard them; the store keeps everything specific to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The callback contract</b> - what <c>onSettled</c> must do, and why. The callback runs on a
/// thread-pool timer thread, never on the thread that wrote the file, and never while this class
/// holds its own lock. It must do three things, in this order:
/// </para>
/// <list type="number">
/// <item><description>Take the <b>store's own</b> lock, check the store's own disposed flag under it, and return if the store is disposed. A <c>Dispose</c> racing the callback then either finishes first (the callback returns having captured nothing) or the callback captures its delegate before <c>Dispose</c> flips the flag - never both.</description></item>
/// <item><description>Under that same lock, rebuild the snapshot and swap it in <b>before</b> capturing the store's event delegate. An observer that reads the store from inside its own handler must see the state the filesystem change just produced, not what was true before it.</description></item>
/// <item><description>Release the lock, <b>then</b> invoke the captured delegate. Raising under the lock would deadlock a subscriber that takes a lock of its own against a concurrent write or rebuild.</description></item>
/// </list>
/// <para>
/// A callback that cannot rebuild (every read attempt failed) simply returns without swapping or
/// raising, keeping the previous snapshot; see <see cref="RetryingFileRead"/>. An exception escaping the
/// callback is unhandled on a timer thread and terminates the process, so it must catch whatever it
/// can expect.
/// </para>
/// <para>
/// <b>Why Error reschedules.</b> <see cref="FileSystemWatcher"/> drops events with no exception and no
/// log when its kernel buffer overflows, and raises <see cref="FileSystemWatcher.Error"/> instead.
/// There is no way to know which change was dropped, so the only correct response is the one a normal
/// change takes: schedule a settle. The internal buffer is raised to 64 KB to make that rarer. The
/// disposed flag is checked <em>before</em> logging, because a watcher event that fires during host
/// teardown, after the logger it would use is gone, must be a no-op rather than a crash.
/// </para>
/// </remarks>
internal sealed class DebouncedFileWatcher : IDisposable
{
    /// <summary>How long after the last filesystem event the callback fires. An editor's save commonly raises several events in a burst; this coalesces them into one callback per pause in activity.</summary>
    public const int DebounceMilliseconds = 500;

    /// <summary>The watcher's kernel buffer. The default is 8 KB, which a burst of events can overflow.</summary>
    private const int InternalBufferSize = 64 * 1024;

    private readonly ILogger logger;
    private readonly string ownerName;
    private readonly Action onSettled;
    private readonly Lock gate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private bool disposed;

    /// <summary>Starts watching <paramref name="directory"/> for names matching <paramref name="filter"/>.</summary>
    /// <param name="directory">The directory to watch; it must already exist.</param>
    /// <param name="filter">The file-name filter, for example <c>appearance.json</c> or <c>*</c>. A single file is watched by its directory plus this filter.</param>
    /// <param name="onSettled">Called once the debounce settles; see the class remarks for what it must do.</param>
    /// <param name="logger">Used to warn when the watcher reports an error.</param>
    /// <param name="ownerName">The owning store's name, used in the Error warning, for example <c>AvatarStore</c>.</param>
    /// <param name="includeSubdirectories">Whether events under subdirectories count too.</param>
    /// <param name="notifyFilter">Which kinds of change raise events; <see langword="null"/> watches last-write and file-name changes.</param>
    public DebouncedFileWatcher(
        string directory,
        string filter,
        Action onSettled,
        ILogger logger,
        string ownerName,
        bool includeSubdirectories = false,
        NotifyFilters? notifyFilter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);
        ArgumentNullException.ThrowIfNull(onSettled);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerName);

        this.onSettled = onSettled;
        this.logger = logger;
        this.ownerName = ownerName;

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        this.watcher = new FileSystemWatcher(directory, filter)
        {
            IncludeSubdirectories = includeSubdirectories,
            InternalBufferSize = InternalBufferSize,
            NotifyFilter = notifyFilter ?? (NotifyFilters.LastWrite | NotifyFilters.FileName),
        };
        this.watcher.Changed += this.OnWatcherEvent;
        this.watcher.Created += this.OnWatcherEvent;
        this.watcher.Deleted += this.OnWatcherEvent;
        this.watcher.Renamed += this.OnWatcherEvent;
        this.watcher.Error += this.OnWatcherError;
        this.watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Stops watching and releases the timer. Idempotent. A callback already running is not waited
    /// for, so the store's own disposed check (see the class remarks) is what stops it.
    /// </summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.watcher.EnableRaisingEvents = false;
        this.watcher.Changed -= this.OnWatcherEvent;
        this.watcher.Created -= this.OnWatcherEvent;
        this.watcher.Deleted -= this.OnWatcherEvent;
        this.watcher.Renamed -= this.OnWatcherEvent;
        this.watcher.Error -= this.OnWatcherError;
        this.watcher.Dispose();
        this.debounceTimer.Dispose();
    }

    /// <summary>Restarts the debounce window, unless disposed.</summary>
    private void Reschedule()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.debounceTimer.Change(DebounceMilliseconds, Timeout.Infinite);
        }
    }

    /// <summary>Any create, change, delete or rename restarts the debounce window.</summary>
    /// <param name="sender">Unused; required by the <see cref="FileSystemWatcher"/> event shape.</param>
    /// <param name="e">Unused; every event counts the same.</param>
    private void OnWatcherEvent(object sender, FileSystemEventArgs e) => this.Reschedule();

    /// <summary>
    /// Logs the watcher's error and schedules a settle, unless disposed. <see langword="internal"/> so a
    /// test can raise it without provoking a real kernel buffer overflow.
    /// </summary>
    /// <param name="sender">Unused; required by the <see cref="FileSystemWatcher.Error"/> event shape.</param>
    /// <param name="e">Carries the exception the watcher caught.</param>
    internal void OnWatcherError(object? sender, ErrorEventArgs e)
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.logger.LogWarning(
                e.GetException(),
                "{Owner}'s FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.",
                this.ownerName);
            this.debounceTimer.Change(DebounceMilliseconds, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }
        }

        this.onSettled();
    }
}
