using Microsoft.Extensions.Options;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Tasks;

/// <summary>The outcome of <see cref="SprintStore.Create(string)"/> (Sprints spec §8).</summary>
internal abstract record SprintCreateResult
{
    /// <summary>A new Sprint was appended to <c>sprints.md</c>.</summary>
    /// <param name="Sprint">The Sprint that was created.</param>
    public sealed record Created(Sprint Sprint) : SprintCreateResult;

    /// <summary>A Sprint with the same name, ignoring case, already existed; nothing was written.</summary>
    /// <param name="Sprint">The Sprint that already had the name.</param>
    public sealed record Existing(Sprint Sprint) : SprintCreateResult;

    /// <summary>The name was invalid, or the file could not be written; nothing was written.</summary>
    /// <param name="Problem">What is wrong, in prose a Human can read as-is.</param>
    public sealed record Refused(string Problem) : SprintCreateResult;
}

/// <summary>
/// Owns <c>{DataDir}/sprints.md</c> (Sprints spec §8): loads and watches it, appends a Sprint line on
/// <see cref="Create(string)"/>, and removes the line of a Sprint a <see cref="TaskEvents.TaskChanged"/> just
/// vacated when no Task holds it any more (<see cref="RemoveIfUnused(SprintId)"/>).
/// </summary>
/// <remarks>
/// <para>
/// The plumbing is <see cref="Views.ViewStore"/>'s: a <see cref="Lock"/> around writes, a
/// <see langword="volatile"/> snapshot, a debounced <see cref="FileSystemWatcher"/> and a 3&#215;20&#160;ms retry on
/// <see cref="IOException"/>. The differences are that a bad line is a <see cref="Problems"/> entry rather than a
/// load error, that writing appends or removes exactly one line, and that this store subscribes to
/// <see cref="TaskEvents.TaskChanged"/> in its constructor and unsubscribes in <see cref="Dispose"/>.
/// </para>
/// <para>
/// A removal looks at one Sprint only, the one just vacated. It never runs on a reload, at startup or when a
/// Task file vanishes, because those are the moments an unreadable drive makes the Task index smaller than the
/// truth and a removal cannot be undone.
/// </para>
/// </remarks>
internal sealed class SprintStore : ISprintCatalog, IDisposable
{
    private const int WatcherDebounceMilliseconds = 500;
    private const int WatcherInternalBufferSize = 64 * 1024;
    private const int WatcherReadRetryAttempts = 3;
    private const int WatcherReadRetryDelayMilliseconds = 20;
    private const int MaxSprintNumber = 999_999_999;
    private const string FileName = "sprints.md";

    private readonly string path;
    private readonly TaskStore tasks;
    private readonly TaskEvents events;
    private readonly ILogger<SprintStore> logger;
    private readonly Lock writeGate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private bool disposed;

    // The whole parsed file, swapped in as one immutable reference so a reader never takes a lock.
    private volatile SprintFile snapshot;

    /// <summary>Loads <c>{DataDir}/sprints.md</c> (an absent file is an empty catalogue), starts watching it and subscribes to <see cref="TaskEvents.TaskChanged"/>.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="tasks">The Task index, read for the highest referenced number and for whether a Sprint is still held.</param>
    /// <param name="events">The hub whose <see cref="TaskEvents.TaskChanged"/> triggers the removal of a vacated Sprint.</param>
    /// <param name="logger">Used to warn about an unreadable file and a failed removal.</param>
    public SprintStore(IOptions<TeamOptions> options, TaskStore tasks, TaskEvents events, ILogger<SprintStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(logger);

        this.tasks = tasks;
        this.events = events;
        this.logger = logger;
        this.path = System.IO.Path.Combine(options.Value.DataDir, FileName);

        string? directory = System.IO.Path.GetDirectoryName(this.path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException($"'{this.path}' has no parent directory to watch.");
        }

        Directory.CreateDirectory(directory);

        this.snapshot = this.LoadInitialSnapshot();

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        this.watcher = new FileSystemWatcher(directory, FileName)
        {
            InternalBufferSize = WatcherInternalBufferSize,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };
        this.watcher.Changed += this.OnWatcherEvent;
        this.watcher.Created += this.OnWatcherEvent;
        this.watcher.Deleted += this.OnWatcherEvent;
        this.watcher.Renamed += this.OnWatcherEvent;
        this.watcher.Error += this.OnWatcherError;
        this.watcher.EnableRaisingEvents = true;

        this.events.TaskChanged += this.OnTaskChanged;
    }

    /// <inheritdoc/>
    public event Action? SprintsChanged;

    /// <inheritdoc/>
    public IReadOnlyList<Sprint> All => this.snapshot.Sprints;

    /// <inheritdoc/>
    public Sprint? Current
    {
        get
        {
            IReadOnlyList<Sprint> sprints = this.snapshot.Sprints;
            return sprints.Count == 0 ? null : sprints[^1];
        }
    }

    /// <summary>Every line that looks like a Sprint and is invalid, with its 1-based line number.</summary>
    public IReadOnlyList<SprintProblem> Problems => this.snapshot.Problems;

    /// <summary>The absolute path of <c>sprints.md</c>, whether or not it exists.</summary>
    public string Path => this.path;

    /// <inheritdoc/>
    public Sprint? Get(SprintId id)
    {
        foreach (Sprint sprint in this.snapshot.Sprints)
        {
            if (sprint.Id == id)
            {
                return sprint;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public Sprint? FindByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string trimmed = name.Trim();
        foreach (Sprint sprint in this.snapshot.Sprints)
        {
            if (string.Equals(sprint.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return sprint;
            }
        }

        return null;
    }

    /// <summary>
    /// Appends a Sprint named <paramref name="name"/> (Sprints spec §8.1), under the write lock, re-reading the file
    /// from disk first so a hand edit a moment ago counts. A name already in use, ignoring case, returns
    /// <see cref="SprintCreateResult.Existing"/> and writes nothing.
    /// </summary>
    /// <param name="name">The new Sprint's name.</param>
    internal SprintCreateResult Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (SprintFileFormat.ValidateName(name) is { } problem)
        {
            return new SprintCreateResult.Refused(problem);
        }

        string trimmed = name.Trim();
        Sprint created = new(new SprintId(1), trimmed);
        Sprint? existing = null;
        bool raise = false;
        lock (this.writeGate)
        {
            try
            {
                string? text = this.ReadTextFromDisk();
                SprintFile file = SprintFileFormat.ParseFile(text ?? "");
                foreach (Sprint sprint in file.Sprints)
                {
                    if (string.Equals(sprint.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        // The Sprint came from a write the watcher has not reported yet: adopt it now, so the Task
                        // save that follows can find it.
                        bool stale = this.snapshot.Sprints.All(known => known.Id != sprint.Id);
                        if (stale)
                        {
                            this.snapshot = file;
                        }

                        existing = sprint;
                        raise = stale;
                        break;
                    }
                }

                if (existing is null)
                {
                    int highestInFile = file.Sprints.Count == 0 ? 0 : file.Sprints[^1].Id.Number;
                    int next = SprintFileFormat.NextNumber(highestInFile, this.HighestReferencedNumber());
                    if (next > MaxSprintNumber)
                    {
                        return new SprintCreateResult.Refused("There are no sprint numbers left.");
                    }

                    created = new Sprint(new SprintId(next), trimmed);
                    string newText = SprintFileFormat.AppendLine(text, created.Id, trimmed);
                    AtomicFile.WriteText(this.path, newText);
                    this.snapshot = SprintFileFormat.ParseFile(newText);
                    raise = true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.logger.LogWarning(ex, "Could not write '{Path}'.", this.path);
                return new SprintCreateResult.Refused($"Could not write '{this.path}': {ex.Message}");
            }
        }

        if (raise)
        {
            this.SprintsChanged?.Invoke();
        }

        return existing is not null
            ? new SprintCreateResult.Existing(existing)
            : new SprintCreateResult.Created(created);
    }

    /// <summary>
    /// Removes the line of <paramref name="id"/> when no Task holds it (Sprints spec §8.2). False when the line
    /// stayed: the Sprint is unknown, a Task file failed to load, a Task (Active or Closed) still holds it, or the
    /// file could not be read or written.
    /// </summary>
    /// <param name="id">The Sprint to remove.</param>
    internal bool RemoveIfUnused(SprintId id)
    {
        lock (this.writeGate)
        {
            if (this.disposed || this.Get(id) is null)
            {
                return false;
            }

            if (this.tasks.RejectedFiles.Count > 0)
            {
                return false;
            }

            foreach (TaskItem task in this.tasks.All)
            {
                if (task.Sprint == id)
                {
                    return false;
                }
            }

            try
            {
                string? text = this.ReadTextFromDisk();
                if (text is null)
                {
                    return false;
                }

                string newText = SprintFileFormat.RemoveLine(text, id);
                if (string.Equals(newText, text, StringComparison.Ordinal))
                {
                    return false;
                }

                AtomicFile.WriteText(this.path, newText);
                this.snapshot = SprintFileFormat.ParseFile(newText);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.logger.LogWarning(ex, "Could not remove the unused sprint {SprintId} from '{Path}'.", id, this.path);
                return false;
            }
        }

        this.SprintsChanged?.Invoke();
        return true;
    }

    /// <summary>Stops watching, unsubscribes from <see cref="TaskEvents.TaskChanged"/> and releases the debounce timer.</summary>
    public void Dispose()
    {
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.events.TaskChanged -= this.OnTaskChanged;
        this.watcher.EnableRaisingEvents = false;
        this.watcher.Changed -= this.OnWatcherEvent;
        this.watcher.Created -= this.OnWatcherEvent;
        this.watcher.Deleted -= this.OnWatcherEvent;
        this.watcher.Renamed -= this.OnWatcherEvent;
        this.watcher.Error -= this.OnWatcherError;
        this.watcher.Dispose();
        this.debounceTimer.Dispose();
    }


    /// <summary>Whether two parses of the file are the same Sprints and the same problems.</summary>
    private static bool SameContent(SprintFile left, SprintFile right) =>
        left.Sprints.SequenceEqual(right.Sprints) && left.Problems.SequenceEqual(right.Problems);

    /// <summary>The file's text, or <see langword="null"/> when it does not exist. Throws <see cref="IOException"/> when it cannot be read.</summary>
    private string? ReadTextFromDisk() => File.Exists(this.path) ? File.ReadAllText(this.path) : null;

    /// <summary>The highest number any Task, Active or Closed, holds, or 0 when none does.</summary>
    private int HighestReferencedNumber()
    {
        int highest = 0;
        foreach (TaskItem task in this.tasks.All)
        {
            if (task.Sprint is { } sprint && sprint.Number > highest)
            {
                highest = sprint.Number;
            }
        }

        return highest;
    }

    /// <summary>Reads the file at construction: no retry, and an unreadable file is an empty catalogue with a warning.</summary>
    private SprintFile LoadInitialSnapshot()
    {
        try
        {
            return SprintFileFormat.ParseFile(this.ReadTextFromDisk() ?? "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(ex, "Could not read '{Path}' at startup; starting with no sprints.", this.path);
            return new SprintFile([], []);
        }
    }

    /// <summary>Acts on a Task that had a Sprint and now has a different one or none (Sprints spec §8.2). A throwing handler is logged and skipped.</summary>
    private void OnTaskChanged(TaskChange change)
    {
        try
        {
            if (change.Before?.Sprint is { } vacated && change.After.Sprint != vacated)
            {
                _ = this.RemoveIfUnused(vacated);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Removing a vacated sprint failed; the sprint stays.");
        }
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e) => this.ScheduleRefresh();

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.logger.LogWarning(
                e.GetException(),
                "SprintStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.");
            this.debounceTimer.Change(WatcherDebounceMilliseconds, Timeout.Infinite);
        }
    }

    private void ScheduleRefresh()
    {
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.debounceTimer.Change(WatcherDebounceMilliseconds, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        bool changed;
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            SprintFile? rebuilt = this.ReadForWatcherRebuild();
            if (rebuilt is null)
            {
                return;
            }

            changed = !SameContent(this.snapshot, rebuilt);
            this.snapshot = rebuilt;
        }

        if (changed)
        {
            this.SprintsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Re-parses the file for an external edit, retrying on <see cref="IOException"/> so an editor holding the file
    /// mid-save does not matter. When every attempt fails the last good snapshot stays (<see langword="null"/>).
    /// </summary>
    private SprintFile? ReadForWatcherRebuild()
    {
        for (int attempt = 1; attempt <= WatcherReadRetryAttempts; attempt++)
        {
            try
            {
                return SprintFileFormat.ParseFile(this.ReadTextFromDisk() ?? "");
            }
            catch (IOException) when (attempt < WatcherReadRetryAttempts)
            {
                Thread.Sleep(WatcherReadRetryDelayMilliseconds);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.logger.LogWarning(ex, "Could not read '{Path}' after a filesystem change, even after retrying; keeping the previous sprints.", this.path);
                return null;
            }
        }

        return null;
    }
}
