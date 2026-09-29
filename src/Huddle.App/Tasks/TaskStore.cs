using System.Collections.Frozen;
using System.Globalization;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Owns every file under <c>{DataDir}/{Teams.Dir}</c>: scans it into an in-memory index by
/// <see cref="TaskId"/>, keeps the list of rejected files, lists every Team folder together with
/// its Projects and orphan status (Spec §8.1-§8.2), and is the only class that writes or moves a
/// Task file (Spec §8.3), watches for edits made outside Huddle (Spec §8.4), and reconciles any
/// edit made while Huddle was stopped once, at startup (Spec §8.5).
/// </summary>
internal sealed partial class TaskStore : IDisposable, ITaskReferenceResolver
{
    /// <summary>The most version history <see cref="GetVersion"/> keeps per Task id (Spec §9.3 step 1).</summary>
    private const int MaxVersionHistory = 20;

    /// <summary>How long the watcher waits after the last filesystem event before rebuilding (Spec §8.4). Copied from <c>PersonaStore.WatcherDebounceMilliseconds</c>.</summary>
    private const int WatcherDebounceMilliseconds = 500;

    /// <summary>
    /// Widened from the .NET default of 8&#160;KB so a burst of changes (a <c>git checkout</c>, a script
    /// touching several Task files at once) is less likely to overflow it and drop events - the same
    /// reasoning as <c>PersonaStore.WatcherInternalBufferSize</c>. An overflow still can't be ruled out,
    /// which is exactly what <see cref="OnWatcherError"/> exists to recover from.
    /// </summary>
    private const int WatcherInternalBufferSize = 64 * 1024;

    private readonly PersonaStore personas;
    private readonly ILogger<TaskStore> logger;

    // Guards every rebuild of `index` - the one lock this class uses for every mutation of its
    // published snapshot, so Write/Move/AppendEntry/WriteMany below and the watcher's debounced
    // rebuild (Task 5.4) share it too, rather than PersonaStore's two-lock design (Settled
    // corrections-B2 D5 item 1).
    private readonly Lock writeGate = new();

    // Keyed with FolderSnapshot.PathComparer (Settled corrections-B2 D5 item 4) so the watcher
    // (Task 5.4) can tell its own writes apart from an outside edit regardless of the file
    // system's case sensitivity.
    private readonly Dictionary<string, string> lastSeenVersion = new(FolderSnapshot.PathComparer);

    // The last MaxVersionHistory versions seen per Task id, oldest first, for the base-version
    // merge in Spec §9.3 step 1. Recorded at the initial scan and after every write (Settled
    // corrections-B2 D5 item 8).
    private readonly Dictionary<TaskId, Queue<TaskItem>> versionHistory = [];

    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;

    private volatile TaskSnapshot index;
    private bool disposed;

    // Set by OnWatcherError, under writeGate, and consumed (and reset) by the next RebuildFromWatcher
    // - so the rebuild that follows a dropped-event buffer overflow always raises IndexChanged, even
    // when it happens to find nothing different from the last known state (Settled corrections-B2 D5
    // item 7: "a forced rebuild from OnWatcherError always raises it").
    private bool forcedRebuildPending;

    /// <summary>
    /// Validates the Teams/Teammates layout with <see cref="LayoutGuard.ValidateTeamsAndTeammates"/>
    /// (kept here, alongside the host's own <c>PostConfigure</c> call, for a host that constructs a
    /// <see cref="TaskStore"/> without going through <c>AddTeamServices</c>), creates the Tasks root
    /// under <see cref="TeamsOptions.Dir"/>, then scans it.
    /// </summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/> and <see cref="TeamsOptions.Dir"/>.</param>
    /// <param name="personas">Supplies the Team labels a Team folder is checked against for §8.2's orphan flag, and its <see cref="PersonaStore.PersonasChanged"/> event.</param>
    /// <param name="clock">Supplies the timestamp for any startup-reconciliation Change log entry (Spec §8.5); the watcher's own debounce timer runs on real time, not this clock.</param>
    /// <param name="logger">Used to warn when a directory can't be enumerated or a file can't be read during the scan.</param>
    public TaskStore(IOptions<TeamOptions> options, PersonaStore personas, TimeProvider clock, ILogger<TaskStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        this.personas = personas;
        this.logger = logger;

        LayoutGuard.ValidateTeamsAndTeammates(options.Value);

        string tasksRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));

        this.RootDirectory = tasksRoot;
        Directory.CreateDirectory(tasksRoot);

        this.index = this.Scan();
        this.LogStrandedMemoryTasks();
        foreach (TaskItem task in this.index.All)
        {
            this.RecordVersion(task);
        }

        this.ReconcileOutsideEdits(clock, options.Value.HumanName);

        this.personas.PersonasChanged += this.OnPersonasChanged;

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        // Filter is "*", not "*.md": a Team folder renamed in Explorer raises a Renamed event whose
        // Name is the directory itself, never matching ".md" - AffectsATaskFile is what restores the
        // narrowing in the handler, the same shape traps.md documents for PersonaStore.
        // IncludeSubdirectories = true is load-bearing on its own (traps.md L115): without it, a Task
        // under a Project sub-folder is found once by this scan and never reloads again.
        this.watcher = new FileSystemWatcher(tasksRoot, "*")
        {
            IncludeSubdirectories = true,
            InternalBufferSize = WatcherInternalBufferSize,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
        };
        this.watcher.Changed += this.OnWatcherEvent;
        this.watcher.Created += this.OnWatcherEvent;
        this.watcher.Deleted += this.OnWatcherEvent;
        this.watcher.Renamed += this.OnWatcherEvent;
        this.watcher.Error += this.OnWatcherError;
        this.watcher.EnableRaisingEvents = true;
    }

    /// <summary>Raised when the watcher's debounced rebuild finds a Task whose content or location changed outside Huddle (Spec §8.4). Never raised for this store's own writes, and never for a plain deletion (Spec E-9). Internal: <see cref="OutsideEdit"/> is internal (Task 6.5, Check-Visibility.ps1).</summary>
    internal event Action<OutsideEdit>? OutsideEditDetected;

    /// <summary>Raised after any rebuild that actually changed something - <see cref="OnPersonasChanged"/>'s orphan recomputation, a write or move, or the watcher's debounced rebuild finding an outside edit, a removal, or a Team/Project folder appearing or disappearing. Never raised for a rebuild that changed nothing, except one forced by <see cref="OnWatcherError"/> (Settled corrections-B2 D5 item 7).</summary>
    public event Action? IndexChanged;

    /// <summary>Every Task that loaded cleanly, as an immutable snapshot.</summary>
    public IReadOnlyList<TaskItem> All => this.index.All;

    /// <summary>Every file under <see cref="RootDirectory"/> that did not become a Task, with its reason.</summary>
    public IReadOnlyList<RejectedTaskFile> RejectedFiles => this.index.Rejected;

    /// <summary>Every Team folder under <see cref="RootDirectory"/>, with its Projects and orphan status.</summary>
    public IReadOnlyList<TeamFolder> Teams => this.index.Teams;

    /// <summary>The absolute path of the Tasks scan root.</summary>
    public string RootDirectory { get; }

    /// <summary>How many times <see cref="RebuildFromWatcher"/> has run, whether or not it found anything different. <c>internal</c> so a test can prove the watcher's filter skips an irrelevant event without inspecting <see cref="IndexChanged"/> timing.</summary>
    internal int RebuildCount { get; private set; }

    /// <summary>Looks up a Task by id. Returns <see langword="null"/>, never throws, when no file carries that id.</summary>
    /// <param name="id">The Task's id.</param>
    public TaskItem? Get(TaskId id) => this.index.ById.GetValueOrDefault(id);

    /// <summary>
    /// Writes <paramref name="text"/> to a brand-new file for <paramref name="task"/>, refusing to
    /// overwrite an existing one (Settled corrections-B2 D6 item 6: Create never overwrites - the id
    /// was allocated for exactly this file, so unlike <see cref="Write"/> there is no prior version
    /// to check against). Added for <c>TaskService</c> (Task 6.1.i); the text must already parse, the
    /// same guard <see cref="Write"/> applies. <paramref name="task"/>'s <see cref="TaskItem.Location"/>
    /// is resolved through <see cref="CanonicalizeLocation"/> before the path is built (ADR-0025: a
    /// Team or Project folder is matched case-insensitively on every OS), so this never creates a
    /// second, differently-cased folder next to one that already exists - even if a future caller,
    /// unlike <c>TaskService.CreateCore</c> today, forgets to canonicalise first.
    /// </summary>
    /// <param name="task">The Task being created; its <see cref="TaskItem.Id"/> and <see cref="TaskItem.Location"/> are used to compute the path actually written to.</param>
    /// <param name="text">The full file text to write, already composed.</param>
    /// <returns>The re-parsed Task as written, or <see langword="null"/> when a file already exists at that path.</returns>
    internal TaskItem? Create(TaskItem task, string text)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(text);

        TaskLocation location = this.CanonicalizeLocation(task.Location);
        string path = TaskLayout.PathFor(this.RootDirectory, location, task.Id);

        if (!TaskFileFormat.TryParse(text, path, location, out _, out string parseError))
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Task '{task.Id}' could not be written: {parseError}"));
        }

        Action? changed;
        TaskItem written;
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return null;
            }

            if (File.Exists(path))
            {
                return null;
            }

            string? directory = Path.GetDirectoryName(path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            string tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, text);
            try
            {
                File.Move(tmpPath, path, overwrite: false);
            }
            catch (IOException)
            {
                // A file appeared at path between the Exists check above and this Move - the
                // same race Create exists to prevent (Settled facts.md R4 "Create collision"). The
                // caller's id allocation was still valid; TaskService maps this to an
                // InvalidOperationException rather than silently overwriting the racing file.
                File.Delete(tmpPath);
                return null;
            }

            written = ReparseWritten(path, location, task.Id, "created");
            this.RecordVersion(written);
            this.index = this.RefreshTeams(ReplaceInIndex(this.index, written));
            changed = this.IndexChanged;
        }

        changed?.Invoke();
        return written;
    }

    /// <summary>
    /// Resolves <paramref name="id"/> for <see cref="Services.MarkdownRenderer"/> (Spec §13.13.2), by
    /// the same lock-free <see cref="FrozenDictionary{TKey,TValue}"/> read as <see cref="Get"/>.
    /// </summary>
    /// <param name="id">The candidate Task id.</param>
    TaskReference? ITaskReferenceResolver.Resolve(TaskId id) =>
        this.Get(id) is { } task ? new TaskReference(task.Id, task.Title, task.Location.Closed) : null;

    /// <summary>
    /// Writes <paramref name="text"/> atomically to <paramref name="task"/>'s file (Spec §8.3), after
    /// first confirming two things: the text parses (Settled corrections-B2 D5 item 9 - nothing
    /// invalid ever reaches disk), and the file on disk is still at <paramref name="expectedVersion"/>
    /// (D5 item 6). A stale <paramref name="expectedVersion"/> is not an error - it means someone else
    /// wrote the file first, so this returns <see langword="null"/> and leaves the file untouched, for
    /// <c>TaskService</c> to map to a merge or a <c>Conflict</c>.
    /// </summary>
    /// <param name="task">The Task being written; its <see cref="TaskItem.Path"/> and <see cref="TaskItem.Location"/> are used.</param>
    /// <param name="expectedVersion">The version <paramref name="task"/> was last seen at.</param>
    /// <param name="text">The full file text to write, already composed.</param>
    /// <returns>The re-parsed Task as written, or <see langword="null"/> on a version conflict.</returns>
    internal TaskItem? Write(TaskItem task, string expectedVersion, string text)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        ArgumentNullException.ThrowIfNull(text);

        if (!TaskFileFormat.TryParse(text, task.Path, task.Location, out _, out string parseError))
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Task '{task.Id}' could not be written: {parseError}"));
        }

        Action? changed;
        TaskItem written;
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return null;
            }

            string? diskVersion = ReadDiskVersion(task.Path);
            if (diskVersion is null || !string.Equals(diskVersion, expectedVersion, StringComparison.Ordinal))
            {
                return null;
            }

            WriteAtomic(task.Path, text);
            written = ReparseWritten(task.Path, task.Location, task.Id, "written");
            this.RecordVersion(written);
            this.index = ReplaceInIndex(this.index, written);
            changed = this.IndexChanged;
        }

        changed?.Invoke();
        return written;
    }

    /// <summary>
    /// Moves <paramref name="task"/> to <paramref name="to"/>, writing <paramref name="text"/> there
    /// (Spec §8.3): create the target directory, refuse an existing target, rename then write the new
    /// text atomically over the target - rolling the rename back if the write fails - and record the
    /// new version. <paramref name="to"/> is resolved through <see cref="CanonicalizeLocation"/> first
    /// (ADR-0025: a Team or Project folder is matched case-insensitively, on every OS), so requesting a
    /// Team or Project by a different casing than an existing folder's reuses that folder instead of
    /// building a second, differently-cased one next to it - the file-system-dependent bug this fixed:
    /// on a case-insensitive file system (Windows, macOS) an unresolved target happened to collide with
    /// the source and looked like a same-folder move, while on a case-sensitive one (Linux) it silently
    /// created a sibling folder. When the resolved target is the source path itself - nothing about the
    /// folder actually changes - this writes in place, the same way <see cref="Write"/> does, rather
    /// than attempting a rename to the file's own path. A source and resolved target that differ only in
    /// case without either being the other - a caller renaming a Team folder's own casing before any
    /// Task exists under the new one - still goes through a temp name, because a plain rename is a no-op
    /// on a case-insensitive file system and a write-then-delete would delete the very file just written
    /// (Settled corrections-B2 D5 item 3). Like <see cref="Write"/>, a version conflict against
    /// <paramref name="expectedVersion"/> returns <see langword="null"/> rather than throwing; an
    /// existing target is a different failure and still throws. Rollback of a failed write after a
    /// successful rename is implemented but not covered by a unit test (see the brief's Coverage note):
    /// forcing that path needs an I/O failure injected between the rename and the write, which this
    /// suite has no seam for.
    /// </summary>
    /// <param name="task">The Task being moved; its current <see cref="TaskItem.Path"/> is the source.</param>
    /// <param name="expectedVersion">The version <paramref name="task"/> was last seen at.</param>
    /// <param name="to">The Task's new location.</param>
    /// <param name="text">The full file text to write at the target, already composed for <paramref name="to"/>.</param>
    /// <returns>The re-parsed Task as written at the target, or <see langword="null"/> on a version conflict.</returns>
    internal TaskItem? Move(TaskItem task, string expectedVersion, TaskLocation to, string text)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(text);

        TaskLocation canonicalTo = this.CanonicalizeLocation(to);

        if (!TaskFileFormat.TryParse(text, task.Path, canonicalTo, out _, out string parseError))
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Task '{task.Id}' could not be written: {parseError}"));
        }

        string targetPath = TaskLayout.PathFor(this.RootDirectory, canonicalTo, task.Id);

        Action? changed;
        TaskItem written;
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return null;
            }

            string? diskVersion = ReadDiskVersion(task.Path);
            if (diskVersion is null || !string.Equals(diskVersion, expectedVersion, StringComparison.Ordinal))
            {
                return null;
            }

            string? targetDirectory = Path.GetDirectoryName(targetPath);
            if (targetDirectory is not null)
            {
                Directory.CreateDirectory(targetDirectory);
            }

            bool sameLocation = string.Equals(task.Path, targetPath, StringComparison.Ordinal);

            if (!sameLocation)
            {
                bool caseOnlyRename = FolderSnapshot.PathComparer.Equals(task.Path, targetPath);

                if (caseOnlyRename)
                {
                    string tempPath = task.Path + ".tmp-move";
                    File.Move(task.Path, tempPath, overwrite: false);
                    File.Move(tempPath, targetPath, overwrite: false);
                }
                else
                {
                    if (File.Exists(targetPath))
                    {
                        throw new IOException(string.Create(
                            CultureInfo.InvariantCulture,
                            $"A file named {Path.GetFileName(targetPath)} already exists in {targetDirectory}."));
                    }

                    File.Move(task.Path, targetPath, overwrite: false);
                }
            }

            try
            {
                WriteAtomic(targetPath, text);
            }
            catch (IOException)
            {
                // Roll back the rename, so a failed write never leaves the Task missing from both
                // the source and the target (Settled corrections-B2 D5 item 2: "On failure of the
                // write, move it back"). Nothing to roll back when the target was the source all along.
                if (!sameLocation)
                {
                    File.Move(targetPath, task.Path, overwrite: false);
                }

                throw;
            }

            written = ReparseWritten(targetPath, canonicalTo, task.Id, "moved");
            this.lastSeenVersion.Remove(task.Path);
            this.RecordVersion(written);
            this.index = this.RefreshTeams(ReplaceInIndex(this.index, written));
            changed = this.IndexChanged;
        }

        changed?.Invoke();
        return written;
    }

    /// <summary>
    /// Writes several Tasks in one batch, under a single acquisition of <see cref="writeGate"/>, and
    /// raises <see cref="IndexChanged"/> at most once for the whole batch rather than once per item
    /// (Settled corrections-B2 D5 item 7 - the Teammate-rename cascade, Task 6.6, writes many files
    /// for one rename and must not flood subscribers with one event per file).
    /// </summary>
    /// <param name="writes">Each Task to write, the version it was last seen at, and its new composed text.</param>
    /// <returns>One result per item, in order: the re-parsed Task, or <see langword="null"/> on a version conflict.</returns>
    internal IReadOnlyList<TaskItem?> WriteMany(IReadOnlyList<(TaskItem Task, string ExpectedVersion, string Text)> writes)
    {
        ArgumentNullException.ThrowIfNull(writes);

        foreach ((TaskItem task, string _, string text) in writes)
        {
            if (!TaskFileFormat.TryParse(text, task.Path, task.Location, out _, out string parseError))
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Task '{task.Id}' could not be written: {parseError}"));
            }
        }

        List<TaskItem?> results = new(writes.Count);
        Action? changed = null;
        lock (this.writeGate)
        {
            if (!this.disposed)
            {
                TaskSnapshot snapshot = this.index;
                bool anyWritten = false;
                foreach ((TaskItem task, string expectedVersion, string text) in writes)
                {
                    string? diskVersion = ReadDiskVersion(task.Path);
                    if (diskVersion is null || !string.Equals(diskVersion, expectedVersion, StringComparison.Ordinal))
                    {
                        results.Add(null);
                        continue;
                    }

                    WriteAtomic(task.Path, text);
                    TaskItem written = ReparseWritten(task.Path, task.Location, task.Id, "written");
                    this.RecordVersion(written);
                    snapshot = ReplaceInIndex(snapshot, written);
                    results.Add(written);
                    anyWritten = true;
                }

                this.index = snapshot;
                if (anyWritten)
                {
                    changed = this.IndexChanged;
                }
            }
        }

        changed?.Invoke();
        return results;
    }

    /// <summary>
    /// Appends <paramref name="entry"/> to <paramref name="id"/>'s file exactly as it is on disk,
    /// without recomposing it (Spec §9.4 "Outside edits" - a hand-edited file's formatting and
    /// unknown keys survive), when the disk is still at <paramref name="expectedVersion"/>.
    /// </summary>
    /// <param name="id">The Task to append to.</param>
    /// <param name="expectedVersion">The version the file was last seen at.</param>
    /// <param name="entry">The Change log entry to append.</param>
    /// <returns>The re-parsed Task after the append, or <see langword="null"/> when the disk no longer matches <paramref name="expectedVersion"/> or the id is unknown.</returns>
    internal TaskItem? AppendEntry(TaskId id, string expectedVersion, ChangeLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(expectedVersion);
        ArgumentNullException.ThrowIfNull(entry);

        Action? changed;
        TaskItem written;
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return null;
            }

            TaskItem? current = this.index.ById.GetValueOrDefault(id);
            if (current is null || !File.Exists(current.Path))
            {
                return null;
            }

            string diskText = File.ReadAllText(current.Path);
            if (!string.Equals(TaskFileFormat.ComputeVersion(diskText), expectedVersion, StringComparison.Ordinal))
            {
                return null;
            }

            string appended = TaskFileFormat.AppendEntry(diskText, entry);
            if (!TaskFileFormat.TryParse(appended, current.Path, current.Location, out _, out string parseError))
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Task '{id}' could not be written: {parseError}"));
            }

            WriteAtomic(current.Path, appended);
            written = ReparseWritten(current.Path, current.Location, id, "written");
            this.RecordVersion(written);
            this.index = ReplaceInIndex(this.index, written);
            changed = this.IndexChanged;
        }

        changed?.Invoke();
        return written;
    }

    /// <summary>The current text of <paramref name="id"/>'s file straight from disk, or <see langword="null"/> when the id is unknown or its file no longer exists.</summary>
    /// <param name="id">The Task to read.</param>
    internal string? ReadText(TaskId id)
    {
        lock (this.writeGate)
        {
            TaskItem? task = this.index.ById.GetValueOrDefault(id);
            return task is not null && File.Exists(task.Path) ? File.ReadAllText(task.Path) : null;
        }
    }

    /// <summary>
    /// Finds <paramref name="id"/> as it was at <paramref name="version"/>, among the last
    /// <see cref="MaxVersionHistory"/> versions recorded for it (Spec §9.3 step 1). Returns
    /// <see langword="null"/> for a version never recorded, or evicted since.
    /// </summary>
    /// <param name="id">The Task id to look up.</param>
    /// <param name="version">The version hash to find.</param>
    internal TaskItem? GetVersion(TaskId id, string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        lock (this.writeGate)
        {
            if (!this.versionHistory.TryGetValue(id, out Queue<TaskItem>? history))
            {
                return null;
            }

            foreach (TaskItem candidate in history)
            {
                if (string.Equals(candidate.Version, version, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The highest Task number seen for <paramref name="prefix"/>, over every parsed Task id and
    /// every file name - valid or rejected - that parses as a <see cref="TaskId"/> with that prefix
    /// (Settled corrections-B2 D6 item 6, needed by <c>TaskIdAllocator.Next</c> so a file copied in
    /// by hand with a higher number can't be reused). Returns 0 when nothing matches.
    /// </summary>
    /// <param name="prefix">The id prefix to search for, compared case-insensitively.</param>
    internal int HighestNumber(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        string normalizedPrefix = prefix.ToUpperInvariant();
        TaskSnapshot snapshot = this.index;
        int highest = 0;

        foreach (TaskItem task in snapshot.All)
        {
            highest = Math.Max(highest, NumberIfMatches(task.Id, normalizedPrefix));
            highest = Math.Max(highest, NumberFromFileName(task.Path, normalizedPrefix));
        }

        foreach (RejectedTaskFile rejected in snapshot.Rejected)
        {
            highest = Math.Max(highest, NumberFromFileName(rejected.Path, normalizedPrefix));
        }

        return highest;
    }

    /// <summary>Unsubscribes from <see cref="PersonaStore.PersonasChanged"/>, and stops and disposes the watcher and its debounce timer.</summary>
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

        this.personas.PersonasChanged -= this.OnPersonasChanged;

        this.watcher.EnableRaisingEvents = false;
        this.watcher.Changed -= this.OnWatcherEvent;
        this.watcher.Created -= this.OnWatcherEvent;
        this.watcher.Deleted -= this.OnWatcherEvent;
        this.watcher.Renamed -= this.OnWatcherEvent;
        this.watcher.Error -= this.OnWatcherError;
        this.watcher.Dispose();
        this.debounceTimer.Dispose();
    }

    /// <summary>
    /// FileSystemWatcher raises <see cref="FileSystemWatcher.Changed"/>, <see cref="FileSystemWatcher.Created"/>,
    /// <see cref="FileSystemWatcher.Deleted"/> and <see cref="FileSystemWatcher.Renamed"/> through this one
    /// handler (the watcher's filter is "*", widened from ".md" so a directory rename is never silently
    /// dropped - traps.md L120), so <see cref="AffectsATaskFile"/> is what keeps a stray non-Task file
    /// (this class's own ".md.tmp" atomic-write artefact included) from churning the debounce.
    /// </summary>
    /// <param name="sender">Unused; required by the event handler shape.</param>
    /// <param name="e">Describes what changed and how.</param>
    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        if (!AffectsATaskFile(this.RootDirectory, e))
        {
            return;
        }

        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.debounceTimer.Change(WatcherDebounceMilliseconds, Timeout.Infinite);
        }
    }

    /// <summary>
    /// True for an event this store cares about: delegates to <see cref="TaskLayout.AffectsTasks"/>,
    /// the pure path-shape predicate that also drives the migration and the reference resolver, so a
    /// Team folder, a Project folder, a Task file or its <c>_tasks</c>/<c>_closed</c> folder is never
    /// judged two different ways. For a <see cref="RenamedEventArgs"/>, the event is relevant when
    /// either <see cref="FileSystemEventArgs.FullPath"/> or <see cref="RenamedEventArgs.OldFullPath"/>
    /// affects Tasks - on Windows a cross-folder move raises Deleted+Created, but a same-Team rename
    /// (a Task file renamed out of <c>_tasks</c> into a sibling notes folder) raises a single Renamed
    /// event whose new path alone would not affect Tasks. <c>internal</c>, not <c>private</c>, and
    /// <c>static</c> with an explicit <paramref name="root"/> so a test can call it directly with a
    /// hand-built event, without a live watcher.
    /// </summary>
    /// <param name="root">The Tasks root directory to resolve paths against.</param>
    /// <param name="e">The watcher event to classify.</param>
    internal static bool AffectsATaskFile(string root, FileSystemEventArgs e) =>
        TaskLayout.AffectsTasks(root, e.FullPath)
        || (e is RenamedEventArgs renamed && TaskLayout.AffectsTasks(root, renamed.OldFullPath));

    /// <summary>
    /// FileSystemWatcher raises this instead of a normal change event when its internal buffer
    /// overflows and the OS drops events (traps.md L128). There is no way to know which paths were
    /// dropped, so the only correct response is the same one a normal change takes: schedule a
    /// refresh on the existing debounce path, flagged so that refresh raises <see cref="IndexChanged"/>
    /// unconditionally (Settled corrections-B2 D5 item 7) rather than trusting a rebuild that happens
    /// to find nothing different. <c>internal</c> rather than <c>private</c> only so <c>Huddle.Tests</c>
    /// can invoke it directly, mirroring <c>PersonaStore.OnWatcherError</c>.
    /// </summary>
    /// <param name="sender">Unused; required by the <see cref="FileSystemWatcher.Error"/> event shape.</param>
    /// <param name="e">Carries the exception the watcher caught.</param>
    internal void OnWatcherError(object sender, ErrorEventArgs e)
    {
        lock (this.writeGate)
        {
            // Checked before logging, not after, for the same reason PersonaStore.OnWatcherError gives:
            // an Error event that fires during host teardown, after this store is disposed, must not
            // throw ObjectDisposedException on the watcher's callback thread.
            if (this.disposed)
            {
                return;
            }

            this.logger.LogWarning(
                e.GetException(),
                "TaskStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.");
            this.forcedRebuildPending = true;
            this.debounceTimer.Change(WatcherDebounceMilliseconds, Timeout.Infinite);
        }
    }

    /// <summary>Runs the debounced rebuild once the 500&#160;ms window since the last relevant filesystem event has elapsed.</summary>
    /// <param name="state">Unused; required by the <see cref="TimerCallback"/> shape.</param>
    private void OnDebounceElapsed(object? state) => this.RebuildFromWatcher();

    /// <summary>
    /// Rescans <see cref="RootDirectory"/>, finds every Task whose content or location changed outside
    /// Huddle since the last rebuild (Spec §8.4) and raises <see cref="OutsideEditDetected"/> once per
    /// one found, then publishes the new snapshot and raises <see cref="IndexChanged"/> - but only when
    /// something actually changed, unless <see cref="forcedRebuildPending"/> says otherwise (Settled
    /// corrections-B2 D5 item 7). <c>internal</c>, not <c>private</c>, so a test can drive the rebuild
    /// core directly rather than only through the real watcher and its debounce.
    /// </summary>
    internal void RebuildFromWatcher()
    {
        Action? indexChanged;
        List<OutsideEdit> edits;
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.RebuildCount++;

            bool forceRaise = this.forcedRebuildPending;
            this.forcedRebuildPending = false;

            TaskSnapshot previous = this.index;
            TaskSnapshot rebuilt = this.Scan();
            bool anyRemoved;
            (edits, anyRemoved) = this.DiffForOutsideEdits(previous, rebuilt);

            bool teamsChanged = !previous.Teams.ToHashSet().SetEquals(rebuilt.Teams);
            bool rejectedChanged = !previous.Rejected.ToHashSet().SetEquals(rebuilt.Rejected);
            bool changed = forceRaise || edits.Count > 0 || anyRemoved || teamsChanged || rejectedChanged;

            this.index = rebuilt;
            indexChanged = changed ? this.IndexChanged : null;
        }

        foreach (OutsideEdit edit in edits)
        {
            this.OutsideEditDetected?.Invoke(edit);
        }

        indexChanged?.Invoke();
    }

    /// <summary>
    /// Compares <paramref name="rebuilt"/> against <paramref name="previous"/> and the live
    /// <see cref="lastSeenVersion"/> map to find every Task whose recorded version no longer matches
    /// what is now on disk (Spec §8.4): unchanged since the last rebuild if the version still matches
    /// (this includes the store's own writes, which already updated <see cref="lastSeenVersion"/> before
    /// any watcher rebuild can run - Spec §4 principle 3), otherwise an outside edit, looked up by id
    /// against <paramref name="previous"/> so a Task moved to a different path is still found and
    /// reported with its old and new state. Records the new version of each one found (Settled
    /// corrections-B2 D5 item 8) and drops the stale <see cref="lastSeenVersion"/> entry for a Task's
    /// old path, whether it moved or disappeared entirely. Callers hold <see cref="writeGate"/>.
    /// </summary>
    /// <param name="previous">The snapshot as it stood before this rebuild.</param>
    /// <param name="rebuilt">The freshly rescanned snapshot.</param>
    /// <returns>Every outside edit found, and whether any previously-known Task disappeared entirely (Spec E-9).</returns>
    private (List<OutsideEdit> Edits, bool AnyRemoved) DiffForOutsideEdits(TaskSnapshot previous, TaskSnapshot rebuilt)
    {
        List<OutsideEdit> edits = [];

        foreach (TaskItem task in rebuilt.All)
        {
            if (this.lastSeenVersion.TryGetValue(task.Path, out string? seenVersion) &&
                string.Equals(seenVersion, task.Version, StringComparison.Ordinal))
            {
                continue;
            }

            TaskItem? previousById = previous.ById.GetValueOrDefault(task.Id);
            if (previousById is not null && !string.Equals(previousById.Path, task.Path, StringComparison.Ordinal))
            {
                this.lastSeenVersion.Remove(previousById.Path);
            }

            edits.Add(new OutsideEdit(previousById, task));
            this.RecordVersion(task);
        }

        bool anyRemoved = false;
        foreach (TaskItem oldTask in previous.All)
        {
            if (!rebuilt.ById.ContainsKey(oldTask.Id))
            {
                this.lastSeenVersion.Remove(oldTask.Path);
                anyRemoved = true;
            }
        }

        return (edits, anyRemoved);
    }

    /// <summary>
    /// Records <paramref name="task"/> as the latest version seen at its path, and pushes it onto its
    /// id's rolling history, evicting the oldest entry past <see cref="MaxVersionHistory"/> (Spec §9.3
    /// step 1; Settled corrections-B2 D5 item 8). Callers hold <see cref="writeGate"/>.
    /// </summary>
    /// <param name="task">The Task to record.</param>
    private void RecordVersion(TaskItem task)
    {
        this.lastSeenVersion[task.Path] = task.Version;

        if (!this.versionHistory.TryGetValue(task.Id, out Queue<TaskItem>? history))
        {
            history = new Queue<TaskItem>();
            this.versionHistory[task.Id] = history;
        }

        history.Enqueue(task);
        while (history.Count > MaxVersionHistory)
        {
            history.Dequeue();
        }
    }

    /// <summary>The version hash of <paramref name="path"/>'s current disk text, or <see langword="null"/> when the file doesn't exist.</summary>
    private static string? ReadDiskVersion(string path) =>
        File.Exists(path) ? TaskFileFormat.ComputeVersion(File.ReadAllText(path)) : null;

    /// <summary>
    /// Startup reconciliation (Spec §8.5, Spec §17 D-16): the initial scan has no previous index to
    /// diff against, so instead every Task whose file was written more than 2 seconds after its last
    /// Change log entry - or that has no entries at all - gains one "edited outside Huddle" entry,
    /// written back silently. Runs once, from the constructor, strictly before the watcher exists, so
    /// there is no lock to take and nothing to raise: no <see cref="OutsideEditDetected"/>, and no one
    /// is woken (D-16 - a bulk edit made while Huddle was stopped, such as a <c>git checkout</c>, must
    /// not flood wake-ups at startup, Settled corrections-B2 D6 item 14).
    /// </summary>
    /// <param name="clock">Supplies the appended entry's timestamp, truncated to the second.</param>
    /// <param name="humanName"><see cref="TeamOptions.HumanName"/>, attributed as the entry's actor - the directory isn't initialised yet when this runs (Spec §8.5).</param>
    private void ReconcileOutsideEdits(TimeProvider clock, string humanName)
    {
        foreach (TaskItem task in this.index.All)
        {
            if (!NeedsReconciliation(task))
            {
                continue;
            }

            string diskText = File.ReadAllText(task.Path);
            ChangeLogEntry entry = new(TruncateToSeconds(clock.GetUtcNow()), humanName, "edited outside Huddle");
            string appended = TaskFileFormat.AppendEntry(diskText, entry);
            if (!TaskFileFormat.TryParse(appended, task.Path, task.Location, out _, out string parseError))
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Task '{task.Id}' could not be reconciled at startup: {parseError}"));
            }

            WriteAtomic(task.Path, appended);
            TaskItem written = ReparseWritten(task.Path, task.Location, task.Id, "reconciled");
            this.RecordVersion(written);
            this.index = ReplaceInIndex(this.index, written);
        }
    }

    /// <summary>True when <paramref name="task"/>'s file was written more than 2 seconds after its last Change log entry's <see cref="ChangeLogEntry.At"/>, or it has no entries at all (Spec §8.5).</summary>
    /// <param name="task">The Task to check, as found by the initial scan.</param>
    private static bool NeedsReconciliation(TaskItem task)
    {
        if (task.ChangeLog.Count == 0)
        {
            return true;
        }

        DateTime lastWriteUtc = File.GetLastWriteTimeUtc(task.Path);
        DateTime lastEntryUtc = task.ChangeLog[^1].At.UtcDateTime;
        return lastWriteUtc > lastEntryUtc + TimeSpan.FromSeconds(2);
    }

    /// <summary>Drops <paramref name="value"/>'s sub-second precision, matching the whole-second granularity <see cref="TaskFileFormat.FormatEntry"/> writes to disk.</summary>
    /// <param name="value">The instant to truncate.</param>
    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Offset);

    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/> atomically: a ".tmp" file, then an overwriting <see cref="File.Move(string, string, bool)"/> (Spec §8.3; the precedent is <c>FileStateStore.cs:173-176</c>).</summary>
    private static void WriteAtomic(string path, string text)
    {
        string tmpPath = path + ".tmp";
        File.WriteAllText(tmpPath, text);
        File.Move(tmpPath, path, overwrite: true);
    }

    /// <summary>
    /// Re-reads and re-parses <paramref name="path"/> right after this class wrote it. A failure here
    /// means the text this class itself just composed and validated didn't survive the round trip -
    /// an invariant break, not a caller mistake, so it throws rather than returning null.
    /// </summary>
    private static TaskItem ReparseWritten(string path, TaskLocation location, TaskId id, string verb)
    {
        string writtenText = File.ReadAllText(path);
        if (!TaskFileFormat.TryParse(writtenText, path, location, out TaskItem? reparsed, out string parseError))
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"Task '{id}' failed to reparse after being {verb}: {parseError}"));
        }

        return reparsed;
    }

    /// <summary>Gives back <paramref name="snapshot"/> with <paramref name="updated"/> replacing its previous entry in <see cref="TaskSnapshot.All"/> and <see cref="TaskSnapshot.ById"/>; <see cref="TaskSnapshot.Rejected"/> and <see cref="TaskSnapshot.Teams"/> are unchanged. A caller whose write can introduce a brand-new Team or Project folder (<see cref="Create"/>, <see cref="Move"/>) must also refresh <see cref="TaskSnapshot.Teams"/> itself, with <see cref="RefreshTeams(TaskSnapshot)"/> - otherwise the folder stays invisible to <see cref="TaskSnapshot.Teams"/> until the watcher's own debounced rebuild notices it, which raises a spurious <see cref="IndexChanged"/> half a second later (the bug this fixed: the debounced rebuild's freshly-scanned Team list no longer matches the stale in-memory one, so <see cref="RebuildFromWatcher"/> sees a real difference and republishes even though nothing changed since the write).</summary>
    private static TaskSnapshot ReplaceInIndex(TaskSnapshot snapshot, TaskItem updated)
    {
        Dictionary<TaskId, TaskItem> byId = new(snapshot.ById);
        byId[updated.Id] = updated;
        List<TaskItem> all = [.. byId.Values];
        return snapshot with { All = all, ById = byId.ToFrozenDictionary() };
    }

    /// <summary>Recomputes <paramref name="snapshot"/>'s <see cref="TaskSnapshot.Teams"/> from disk (the same <see cref="BuildTeams"/>/<see cref="RecomputeOrphans"/> pair <see cref="Scan"/> uses), so a Team or Project folder <see cref="Create"/> or <see cref="Move"/> just created is reflected immediately rather than only after the watcher's own debounced rebuild finds it (see <see cref="ReplaceInIndex"/>'s remarks). Callers hold <see cref="writeGate"/>.</summary>
    /// <param name="snapshot">The snapshot to refresh.</param>
    private TaskSnapshot RefreshTeams(TaskSnapshot snapshot) =>
        snapshot with { Teams = this.RecomputeOrphans(this.BuildTeams().Teams) };

    /// <summary>The Task's <see cref="TaskId.Number"/> when its prefix equals <paramref name="normalizedPrefix"/> (already upper-cased), else 0.</summary>
    private static int NumberIfMatches(TaskId id, string normalizedPrefix) =>
        string.Equals(id.Prefix, normalizedPrefix, StringComparison.Ordinal) ? id.Number : 0;

    /// <summary>
    /// The parsed <see cref="TaskId.Number"/> of <paramref name="path"/>'s file name when it parses as
    /// a <see cref="TaskId"/> with prefix <paramref name="normalizedPrefix"/> (already upper-cased),
    /// else 0. The file name need not match the Task's actual id (Spec §8.1: "the filename isn't
    /// identity") - this still reserves the number a hand-copied file's name carries.
    /// </summary>
    private static int NumberFromFileName(string path, string normalizedPrefix)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        return TaskId.TryParse(fileName, out TaskId parsedId) ? NumberIfMatches(parsedId, normalizedPrefix) : 0;
    }

    /// <summary>
    /// Scans <see cref="RootDirectory"/> into a fresh <see cref="TaskSnapshot"/>: the Team folders
    /// (Spec §8.2), then every ".md" file mapped, read and parsed (Spec §8.1). The only place this
    /// class touches the filesystem to build the index. Deliberately does not record any version
    /// itself - the constructor does that once for the whole initial scan, and
    /// <see cref="DiffForOutsideEdits"/> does it selectively for the watcher's rebuild, so a rebuild
    /// that finds nothing new never touches <see cref="versionHistory"/> at all.
    /// </summary>
    private TaskSnapshot Scan()
    {
        (List<TeamFolder> rawTeams, Dictionary<string, string> aliasToCanonical) = this.BuildTeams();

        List<(TaskItem Task, string Path)> parsed = [];
        List<RejectedTaskFile> rejected = [];

        List<string> files = this.EnumerateTaskFiles();

        foreach (string path in files)
        {
            if (!TaskLayout.TryMap(this.RootDirectory, path, out TaskLocation? location, out _))
            {
                continue;
            }

            if (location is null)
            {
                continue;
            }

            if (aliasToCanonical.TryGetValue(location.Team, out string? canonical) &&
                !string.Equals(canonical, location.Team, StringComparison.Ordinal))
            {
                rejected.Add(new RejectedTaskFile(
                    path,
                    $"Team folder '{location.Team}' duplicates '{canonical}' (case-insensitive); its files are rejected."));
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                rejected.Add(new RejectedTaskFile(path, $"could not be read: {ex.Message}"));
                continue;
            }

            if (!TaskFileFormat.TryParse(text, path, location, out TaskItem? task, out string parseError))
            {
                rejected.Add(new RejectedTaskFile(path, parseError));
                continue;
            }

            parsed.Add((task, path));
        }

        List<TaskItem> all = [];
        Dictionary<TaskId, TaskItem> byId = [];
        foreach (IGrouping<TaskId, (TaskItem Task, string Path)> group in parsed.GroupBy(item => item.Task.Id))
        {
            List<(TaskItem Task, string Path)> items = group.ToList();
            if (items.Count == 1)
            {
                all.Add(items[0].Task);
                byId[items[0].Task.Id] = items[0].Task;
                continue;
            }

            foreach ((TaskItem Task, string Path) item in items)
            {
                string othersJoined = string.Join(
                    ", ",
                    items.Where(other => !string.Equals(other.Path, item.Path, StringComparison.Ordinal)).Select(other => other.Path));
                rejected.Add(new RejectedTaskFile(item.Path, $"duplicate id {item.Task.Id}, also in {othersJoined}"));
            }
        }

        return new TaskSnapshot(all, byId.ToFrozenDictionary(), rejected, this.RecomputeOrphans(rawTeams));
    }

    /// <summary>
    /// The candidate Task file paths for <see cref="Scan"/>: only <c>{Team}/_tasks</c>,
    /// <c>{Team}/_tasks/_closed</c>, <c>{Team}/{Project}/_tasks</c> and
    /// <c>{Team}/{Project}/_tasks/_closed</c> (ADR-0030), each listed with
    /// <see cref="SearchOption.TopDirectoryOnly"/>. A Project's note tree - however deep - is never
    /// descended into, so a folder inside it that cannot be listed never raises a warning. Every
    /// case-variant Team folder is scanned, not only the one <see cref="BuildTeams"/> keeps as
    /// canonical, so a losing duplicate's own files are still found for rejection.
    /// </summary>
    private List<string> EnumerateTaskFiles()
    {
        List<string> files = [];
        foreach (string teamDir in this.RawTeamDirectories())
        {
            this.AddTasksFolderFiles(teamDir, files);

            foreach (string projectDir in this.RawSubfolders(teamDir))
            {
                this.AddTasksFolderFiles(projectDir, files);
            }
        }

        return files;
    }

    /// <summary>Appends the <c>*.md</c> files directly under <paramref name="containerDir"/>'s
    /// <c>_tasks</c> folder and that folder's <c>_closed</c> sub-folder to <paramref name="files"/>.</summary>
    /// <param name="containerDir">The Team or Project folder that may hold a <c>_tasks</c> folder.</param>
    /// <param name="files">The list to append matching file paths to.</param>
    private void AddTasksFolderFiles(string containerDir, List<string> files)
    {
        string tasksDir = Path.Combine(containerDir, TaskLayout.TasksFolder);
        this.AddFilesIfExists(tasksDir, files);
        this.AddFilesIfExists(Path.Combine(tasksDir, TaskLayout.ClosedFolder), files);
    }

    /// <summary>Appends <paramref name="dir"/>'s <c>*.md</c> files (<see cref="SearchOption.TopDirectoryOnly"/>)
    /// to <paramref name="files"/> when <paramref name="dir"/> exists, warning and adding none on a
    /// listing failure.</summary>
    /// <param name="dir">The folder to list.</param>
    /// <param name="files">The list to append matching file paths to.</param>
    private void AddFilesIfExists(string dir, List<string> files)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        try
        {
            files.AddRange(Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(ex, "TaskStore could not enumerate '{Dir}'; treating it as empty.", dir);
        }
    }

    /// <summary>
    /// Logs one Warning per Team folder whose reserved <see cref="TeamNames.MemoryFolder"/> folder (any
    /// case) still holds Task files under <c>_tasks</c> or <c>_tasks/_closed</c> - Tasks a Human filed
    /// there before <c>memory</c> became reserved (Spec §12 E-2). Read-only: it moves nothing. Called once
    /// from the constructor, never from <see cref="Scan"/> (which re-runs on every watcher rebuild) or
    /// <see cref="BuildTeams"/>. Stranded Tasks are not loaded, so they leave <c>All</c> and <c>Rejected</c>
    /// and <c>HighestNumber</c> no longer sees their numbers: the persisted counter protects Huddle-created
    /// numbers, but a hand-copied higher-numbered file could have its number reused (accepted).
    /// </summary>
    private void LogStrandedMemoryTasks()
    {
        foreach (string teamDir in this.RawTeamDirectories())
        {
            List<string> files = [];
            foreach (string memoryDir in this.RawSubfolders(teamDir))
            {
                if (string.Equals(Path.GetFileName(memoryDir), TeamNames.MemoryFolder, StringComparison.OrdinalIgnoreCase))
                {
                    this.AddTasksFolderFiles(memoryDir, files);
                }
            }

            if (files.Count == 0)
            {
                continue;
            }

            string team = Path.GetFileName(teamDir);
            string fileNames = string.Join(", ", files.Select(Path.GetFileName).Order(StringComparer.Ordinal));
            string message = $"Team folder '{team}' has Tasks under 'memory/_tasks/' ({fileNames}); 'memory' is reserved for Team Memory, so they are not loaded. Move them to '{team}/_tasks/' or into a Project.";
            this.logger.LogWarning("{Message}", message);
        }
    }

    /// <summary>The full paths of <paramref name="parent"/>'s non-reserved (<see cref="TaskLayout.IsReservedFolderName"/>)
    /// direct sub-folders, every case variant included.</summary>
    /// <param name="parent">The folder to list.</param>
    private List<string> RawSubfolders(string parent)
    {
        try
        {
            return Directory.GetDirectories(parent)
                .Where(dir => !TaskLayout.IsReservedFolderName(Path.GetFileName(dir) ?? string.Empty))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(ex, "TaskStore could not list folders under '{Parent}'.", parent);
            return [];
        }
    }

    /// <summary>The full paths of <see cref="RootDirectory"/>'s non-reserved Team folders, every case
    /// variant included (unlike <see cref="BuildTeams"/>'s de-duplicated <see cref="TeamFolder"/> list).</summary>
    private List<string> RawTeamDirectories() => this.RawSubfolders(this.RootDirectory);

    /// <summary>
    /// Enumerates Team folders directly under <see cref="RootDirectory"/> with their Project
    /// sub-folders (<see cref="Directory.GetDirectories(string)"/>, two levels - empty folders
    /// count, Spec §8.2 / Settled corrections-B2 D5 item 14). Folders differing only by case fold
    /// into one entry, the first by Ordinal winning; the alias map lets <see cref="Scan"/> reject
    /// files found under a losing folder without reading them.
    /// </summary>
    private (List<TeamFolder> Teams, Dictionary<string, string> AliasToCanonical) BuildTeams()
    {
        List<IGrouping<string, string>> groups = this.RawTeamDirectories()
            .Select(Path.GetFileName)
            .Where(name => name is { Length: > 0 })
            .Select(name => name!)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<TeamFolder> teams = [];
        Dictionary<string, string> aliasToCanonical = new(StringComparer.Ordinal);

        foreach (IGrouping<string, string> group in groups)
        {
            List<string> names = group.OrderBy(name => name, StringComparer.Ordinal).ToList();
            string winner = names[0];
            foreach (string name in names)
            {
                aliasToCanonical[name] = winner;
            }

            teams.Add(new TeamFolder(winner, this.ListProjects(Path.Combine(this.RootDirectory, winner)), IsOrphan: false));
        }

        return (teams, aliasToCanonical);
    }

    /// <summary>The names of <paramref name="teamPath"/>'s Project sub-folders, excluding any name
    /// reserved for Projects (<see cref="TeamNames.IsReservedProjectName"/>) - so the Team's own
    /// <c>_tasks</c> and <c>_closed</c> folders, and its <c>memory</c> folder in any case, are never
    /// listed as Projects.</summary>
    private List<string> ListProjects(string teamPath) =>
        this.RawSubfolders(teamPath)
            .Select(Path.GetFileName)
            .Where(name => name is { Length: > 0 })
            .Select(name => name!)
            .Where(name => !TeamNames.IsReservedProjectName(name))
            .ToList();

    /// <summary>
    /// Resolves <paramref name="location"/>'s Team, and Project when it has one, to an existing Team or
    /// Project folder's on-disk casing, matched case-insensitively (ADR-0025 "a Team folder is a folder
    /// by convention": "the comparison ignores case, because Windows paths do" - a rule <see cref="Teams"/>
    /// already states holds "on every OS", not only the ones that happen to fold case for free). Reads
    /// the live <see cref="Teams"/> snapshot without taking <see cref="writeGate"/>, the same lock-free
    /// pattern <see cref="Get"/> uses. A Team with no existing folder yet, or a Project with no existing
    /// sub-folder under its (now-resolved) Team, passes through unchanged - it is about to become the
    /// new canonical casing itself.
    /// </summary>
    /// <param name="location">The location as requested by a caller, in whatever casing it used.</param>
    private TaskLocation CanonicalizeLocation(TaskLocation location)
    {
        foreach (TeamFolder folder in this.Teams)
        {
            if (!string.Equals(folder.Name, location.Team, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (location.Project is not { Length: > 0 } project)
            {
                return location with { Team = folder.Name };
            }

            foreach (string existingProject in folder.Projects)
            {
                if (string.Equals(existingProject, project, StringComparison.OrdinalIgnoreCase))
                {
                    return location with { Team = folder.Name, Project = existingProject };
                }
            }

            return location with { Team = folder.Name };
        }

        return location;
    }

    /// <summary>Recomputes each folder's <see cref="TeamFolder.IsOrphan"/> against the live <see cref="PersonaStore.Teams"/> list, without rescanning any file (Spec §8.2).</summary>
    /// <param name="teams">The Team folders to recompute orphan status for.</param>
    private List<TeamFolder> RecomputeOrphans(IReadOnlyList<TeamFolder> teams)
    {
        IReadOnlyList<string> personaTeams = this.personas.Teams;
        List<TeamFolder> updated = new(teams.Count);
        foreach (TeamFolder folder in teams)
        {
            bool isOrphan = !personaTeams.Any(team => string.Equals(team, folder.Name, StringComparison.OrdinalIgnoreCase));
            updated.Add(folder with { IsOrphan = isOrphan });
        }

        return updated;
    }

    /// <summary>
    /// Recomputes every Team folder's orphan flag and republishes the snapshot, raising
    /// <see cref="IndexChanged"/> outside the lock, but only when a flag actually flipped. Without
    /// that check this fired on every <see cref="PersonaStore.PersonasChanged"/>, including the
    /// redundant one <see cref="PersonaStore"/>'s own debounced watcher raises ~500&#160;ms after a
    /// Persona write it already knew about (the same shape of bug <see cref="RefreshTeams"/> fixes for
    /// <see cref="Create"/> and <see cref="Move"/>) - a spurious index reload a UI component such as
    /// <c>TaskDetail</c>, subscribed to reloads, would visibly re-render for.
    /// </summary>
    private void OnPersonasChanged()
    {
        Action? changed;
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            TaskSnapshot previous = this.index;
            List<TeamFolder> recomputed = this.RecomputeOrphans(previous.Teams);
            bool teamsChanged = !previous.Teams.SequenceEqual(recomputed);
            this.index = previous with { Teams = recomputed };
            changed = teamsChanged ? this.IndexChanged : null;
        }

        changed?.Invoke();
    }

    /// <summary>An immutable published snapshot of the scan: every valid Task, the rejected files, and the Team folders.</summary>
    /// <param name="All">Every Task that loaded cleanly.</param>
    /// <param name="ById">The same Tasks, keyed by id for <see cref="Get"/>.</param>
    /// <param name="Rejected">Every file that did not become a Task.</param>
    /// <param name="Teams">Every Team folder, with its Projects and orphan status.</param>
    private sealed record TaskSnapshot(
        IReadOnlyList<TaskItem> All,
        FrozenDictionary<TaskId, TaskItem> ById,
        IReadOnlyList<RejectedTaskFile> Rejected,
        IReadOnlyList<TeamFolder> Teams);
}
