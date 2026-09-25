using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>A parse failure in <c>views.json</c>, with a 1-based line and column.</summary>
/// <param name="Message">A human-readable description of the failure.</param>
/// <param name="Line">The 1-based line the failure was found on, or <see langword="null"/> when <see cref="JsonException"/> did not report one.</param>
/// <param name="Column">The 1-based column the failure was found on, or <see langword="null"/> when <see cref="JsonException"/> did not report one.</param>
public sealed record ViewLoadError(string Message, long? Line, long? Column);

/// <summary>
/// Joins the two built-in Views (<c>All Tasks</c>, <c>My Tasks</c>) with the Human-editable
/// <c>{DataDir}/views.json</c>, resolving the current set of Views every render reads through
/// <see cref="Views"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately <see cref="Agency.Huddle.App.Avatars.AvatarStore"/>'s sibling</b>, with four
/// differences the Spec (§12.3) calls out on purpose:
/// </para>
/// <list type="number">
/// <item><description>
/// A malformed file does not fall back to empty. It sets <see cref="LoadError"/>, keeps the last
/// good snapshot (empty at startup - nothing has ever loaded yet), and refuses every write, so a
/// <see cref="Save(TaskView)"/> made while the file is broken can never overwrite the Human's Views
/// with a smaller set than they actually have.
/// </description></item>
/// <item><description>
/// Writing goes through a temporary file plus an atomic <see cref="File.Move(string, string, bool)"/>
/// rather than <see cref="File.WriteAllText(string, string?)"/> directly, so a crash mid-save cannot
/// truncate <c>views.json</c>.
/// </description></item>
/// <item><description>
/// An entry that fails to parse or fails <see cref="ViewValidator"/> is kept - not skipped - and
/// reported through <see cref="InvalidViews"/> with its reason. Its raw JSON is written back
/// unchanged by every later <see cref="Save(TaskView)"/>, so a save can never quietly delete an
/// entry the Human hand-edited into an invalid shape.
/// </description></item>
/// <item><description>
/// <c>All Tasks</c> (<c>all-tasks</c>) and <c>My Tasks</c> (<c>my-tasks</c>, filtered to
/// <c>@me</c>) are synthesised in memory whenever the file does not carry them, and are only ever
/// written once a Human edits one - an absent file is normal (rules.md L58), and nothing here
/// creates one just to read it.
/// </description></item>
/// </list>
/// <para>
/// Everything else mirrors <see cref="Agency.Huddle.App.Avatars.AvatarStore"/>: a
/// <see cref="Lock"/> around writes, a <see langword="volatile"/> snapshot, a debounced
/// <see cref="FileSystemWatcher"/> with no own-write suppression (a <see cref="Save(TaskView)"/>
/// raises <see cref="ViewsChanged"/> once synchronously, and the watcher's redundant refresh of the
/// same write raises it again roughly <see cref="WatcherDebounceMilliseconds"/> later), and a
/// 3&#215;20&#160;ms retry on <see cref="IOException"/> when a watcher-triggered rebuild races an
/// editor mid-save.
/// </para>
/// </remarks>
internal sealed partial class ViewStore : IDisposable
{
    /// <summary>The id of the built-in View showing every open and closed Task.</summary>
    internal const string AllTasksId = "all-tasks";

    /// <summary>The id of the built-in View showing the signed-in Human's own Tasks.</summary>
    internal const string MyTasksId = "my-tasks";

    // Same reasoning and the same value as AvatarStore.WatcherDebounceMilliseconds.
    private const int WatcherDebounceMilliseconds = 500;

    // Same reasoning and the same value as AvatarStore.WatcherInternalBufferSize.
    private const int WatcherInternalBufferSize = 64 * 1024;

    // Same reasoning and the same values as AvatarStore.WatcherReadRetryAttempts/Delay.
    private const int WatcherReadRetryAttempts = 3;
    private const int WatcherReadRetryDelayMilliseconds = 20;

    private readonly string path;
    private readonly ILogger<ViewStore> logger;
    private readonly Lock writeGate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private bool disposed;

    // The whole resolved state, swapped in as one immutable reference so a reader never takes a
    // lock - see AvatarStore's remarks for why this matters for a store read from Razor renders.
    private volatile Snapshot snapshot;

    /// <summary>Loads (or defaults) the override file at <c>{DataDir}/views.json</c>.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>, already absolutised by <c>ServiceCollectionExtensions</c>'s <c>PostConfigure</c>.</param>
    /// <param name="logger">Used to warn when a watcher-triggered rebuild fails to parse the file.</param>
    public ViewStore(IOptions<TeamOptions> options, ILogger<ViewStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.path = Path.Combine(options.Value.DataDir, "views.json");

        string? directory = Path.GetDirectoryName(this.path);
        if (string.IsNullOrEmpty(directory))
        {
            // TeamOptions.DataDir is always absolutised before this constructor runs (see
            // AvatarStore's identical guard), so this only exists so the watcher below always
            // has a real directory.
            throw new InvalidOperationException($"'{this.path}' has no parent directory to watch.");
        }

        Directory.CreateDirectory(directory);

        this.snapshot = LoadInitialSnapshot(this.path);

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        this.watcher = new FileSystemWatcher(directory, "views.json")
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
    }

    /// <summary>
    /// Raised after <see cref="Save(TaskView)"/>, <see cref="Delete(string)"/> or
    /// <see cref="RenameTeammate(string, string)"/> has written the file and rebuilt the resolved
    /// snapshot, and after an external edit to <see cref="FilePath"/> is picked up by the
    /// filesystem watcher and its debounce settles. There is no own-write suppression - see the
    /// class remarks - so one call to <see cref="Save(TaskView)"/> raises this event twice: once
    /// synchronously before it returns, and once more roughly <see cref="WatcherDebounceMilliseconds"/>
    /// later from the watcher's own, redundant rebuild of the same write.
    /// </summary>
    public event Action? ViewsChanged;

    /// <summary>The absolute path to the override file, <c>views.json</c> under <see cref="TeamOptions.DataDir"/>, whether or not it currently exists.</summary>
    public string FilePath => this.path;

    /// <summary>The resolved Views: the two built-ins first, then the file's own Views in file order.</summary>
    public IReadOnlyList<TaskView> Views => this.snapshot.Views;

    /// <summary>Every View entry that failed to parse or failed <see cref="ViewValidator"/>, kept rather than dropped.</summary>
    public IReadOnlyList<InvalidView> InvalidViews => this.snapshot.InvalidViews;

    /// <summary>The current parse failure for <c>views.json</c>, or <see langword="null"/> when the file last parsed cleanly (or does not exist).</summary>
    public ViewLoadError? LoadError => this.snapshot.LoadError;

    /// <summary>The View with <paramref name="id"/>, or <see langword="null"/> when there is none.</summary>
    /// <param name="id">The View's id, matched <see cref="StringComparison.Ordinal"/>.</param>
    public TaskView? Get(string id)
    {
        foreach (TaskView view in this.snapshot.Views)
        {
            if (string.Equals(view.Id, id, StringComparison.Ordinal))
            {
                return view;
            }
        }

        return null;
    }

    /// <summary>
    /// Adds or replaces <paramref name="view"/> by its <see cref="TaskView.Id"/>, under the write
    /// lock: refused outright while <see cref="LoadError"/> is set (see the class remarks) or when
    /// <paramref name="view"/> fails <see cref="ViewValidator"/>; otherwise re-reads the file so a
    /// concurrent hand edit - including another View's invalid raw entry - is not lost, writes
    /// through a temporary file and an atomic move, rebuilds the resolved snapshot, releases the
    /// lock, and only then raises <see cref="ViewsChanged"/>.
    /// </summary>
    /// <param name="view">The View to save.</param>
    public ViewSaveResult Save(TaskView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        lock (this.writeGate)
        {
            if (this.snapshot.LoadError is { } loadError)
            {
                return new ViewSaveResult(false, [$"'{this.path}' has a parse error ({loadError.Message}) and must be fixed by hand before a View can be saved."]);
            }

            IReadOnlyList<string> problems = ViewValidator.Validate(view, this.snapshot.Views);
            if (problems.Count > 0)
            {
                return new ViewSaveResult(false, problems);
            }

            ReadResult current = ReadDocumentFromDisk(this.path);
            if (current.LoadError is { } concurrentLoadError)
            {
                return new ViewSaveResult(false, [$"'{this.path}' has a parse error ({concurrentLoadError.Message}) and must be fixed by hand before a View can be saved."]);
            }

            JsonArray entries = current.Entries;
            RemoveEntryById(entries, view.Id);
            entries.Add(JsonSerializer.SerializeToNode(view, ViewJson.Options));

            WriteDocumentToDisk(this.path, entries);
            this.snapshot = BuildSnapshot(entries);
        }

        this.ViewsChanged?.Invoke();
        return new ViewSaveResult(true, []);
    }

    /// <summary>
    /// Removes the View with <paramref name="id"/>, under the write lock. Refused, with no write,
    /// for a built-in id (<see cref="AllTasksId"/>, <see cref="MyTasksId"/>) or while
    /// <see cref="LoadError"/> is set. A no-op, with no write and no <see cref="ViewsChanged"/>,
    /// when <paramref name="id"/> names no entry currently on disk.
    /// </summary>
    /// <param name="id">The id of the View to remove.</param>
    public ViewSaveResult Delete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (IsBuiltIn(id))
        {
            return new ViewSaveResult(false, ["A built-in View cannot be deleted."]);
        }

        lock (this.writeGate)
        {
            if (this.snapshot.LoadError is { } loadError)
            {
                return new ViewSaveResult(false, [$"'{this.path}' has a parse error ({loadError.Message}) and must be fixed by hand before a View can be deleted."]);
            }

            ReadResult current = ReadDocumentFromDisk(this.path);
            if (current.LoadError is { } concurrentLoadError)
            {
                return new ViewSaveResult(false, [$"'{this.path}' has a parse error ({concurrentLoadError.Message}) and must be fixed by hand before a View can be deleted."]);
            }

            JsonArray entries = current.Entries;
            if (!RemoveEntryById(entries, id))
            {
                return new ViewSaveResult(true, []);
            }

            WriteDocumentToDisk(this.path, entries);
            this.snapshot = BuildSnapshot(entries);
        }

        this.ViewsChanged?.Invoke();
        return new ViewSaveResult(true, []);
    }

    /// <summary>
    /// Rewrites <paramref name="oldName"/> to <paramref name="newName"/> in every View's
    /// <see cref="TaskFilter.Assignees"/> (Spec §9.6) - never touching the <c>@me</c> or
    /// <c>@unassigned</c> tokens, which name no Teammate. A no-op, with no write and no
    /// <see cref="ViewsChanged"/>, while <see cref="LoadError"/> is set or when no View's filter
    /// names <paramref name="oldName"/>.
    /// </summary>
    /// <param name="oldName">The Teammate's current Name, matched <see cref="StringComparison.OrdinalIgnoreCase"/>.</param>
    /// <param name="newName">The Teammate's new Name.</param>
    internal void RenameTeammate(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        lock (this.writeGate)
        {
            if (this.snapshot.LoadError is not null)
            {
                return;
            }

            List<TaskView> changed = [];
            foreach (TaskView view in this.snapshot.Views)
            {
                if (!view.Filter.Assignees.Any(assignee => string.Equals(assignee, oldName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                List<string> assignees = [];
                foreach (string assignee in view.Filter.Assignees)
                {
                    assignees.Add(string.Equals(assignee, oldName, StringComparison.OrdinalIgnoreCase) ? newName : assignee);
                }

                changed.Add(view with { Filter = view.Filter with { Assignees = assignees } });
            }

            if (changed.Count == 0)
            {
                return;
            }

            ReadResult current = ReadDocumentFromDisk(this.path);
            if (current.LoadError is not null)
            {
                return;
            }

            JsonArray entries = current.Entries;
            foreach (TaskView view in changed)
            {
                RemoveEntryById(entries, view.Id);
                entries.Add(JsonSerializer.SerializeToNode(view, ViewJson.Options));
            }

            WriteDocumentToDisk(this.path, entries);
            this.snapshot = BuildSnapshot(entries);
        }

        this.ViewsChanged?.Invoke();
    }

    /// <summary>Whether <paramref name="id"/> names one of the two built-in Views.</summary>
    private static bool IsBuiltIn(string id) =>
        string.Equals(id, AllTasksId, StringComparison.Ordinal) || string.Equals(id, MyTasksId, StringComparison.Ordinal);

    /// <summary>Removes the entry whose <c>id</c> property equals <paramref name="id"/>, if any. Returns whether an entry was removed.</summary>
    private static bool RemoveEntryById(JsonArray entries, string id)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index] is JsonObject entry &&
                entry.TryGetPropertyValue("id", out JsonNode? idNode) &&
                idNode is JsonValue idValue &&
                idValue.TryGetValue(out string? entryId) &&
                string.Equals(entryId, id, StringComparison.Ordinal))
            {
                entries.RemoveAt(index);
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds the initial snapshot at construction time: no retry, since there is no previous snapshot to race against.</summary>
    private static Snapshot LoadInitialSnapshot(string path)
    {
        ReadResult result = ReadDocumentFromDisk(path);
        return result.LoadError is { } loadError ? Snapshot.WithLoadError(loadError) : BuildSnapshot(result.Entries);
    }

    /// <summary>
    /// Reads and parses <paramref name="path"/>'s <c>views</c> array. A missing file yields an
    /// empty array (the normal first-run case); malformed JSON yields <see cref="ReadResult.LoadError"/>
    /// instead of throwing, so every caller can react to a parse failure without a
    /// <see langword="try"/>/<see langword="catch"/> of its own.
    /// </summary>
    private static ReadResult ReadDocumentFromDisk(string path)
    {
        if (!File.Exists(path))
        {
            return new ReadResult([], null);
        }

        try
        {
            string json = File.ReadAllText(path);
            JsonNode? root = JsonNode.Parse(json);
            JsonArray entries = root is JsonObject topLevel && topLevel.TryGetPropertyValue("views", out JsonNode? viewsNode) && viewsNode is JsonArray array
                ? array
                : [];
            return new ReadResult(entries, null);
        }
        catch (JsonException ex)
        {
            return new ReadResult([], ToLoadError(path, ex));
        }
    }

    /// <summary>Serialises <paramref name="entries"/> to <paramref name="path"/> through a temporary file plus an atomic move, so a crash mid-save cannot truncate the target.</summary>
    private static void WriteDocumentToDisk(string path, JsonArray entries)
    {
        JsonObject document = new() { ["version"] = 1, ["views"] = entries.DeepClone() };
        string json = document.ToJsonString(ViewJson.Options);
        string tmpPath = path + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, path, overwrite: true);
    }

    /// <summary>
    /// Deserialises each entry in <paramref name="entries"/> into a <see cref="TaskView"/>,
    /// keeping - never dropping - one that fails to parse or fails <see cref="ViewValidator"/> as
    /// an <see cref="InvalidView"/>. Merges in the two built-in Views wherever the file does not
    /// already carry them, built-ins first.
    /// </summary>
    private static Snapshot BuildSnapshot(JsonArray entries)
    {
        List<TaskView> fileViews = [];
        List<InvalidView> invalidViews = [];

        foreach (JsonNode? entry in entries)
        {
            if (entry is not JsonObject entryObject)
            {
                invalidViews.Add(new InvalidView(null, null, ["Each entry in 'views' must be a JSON object."]));
                continue;
            }

            string? entryId = ReadString(entryObject, "id");
            string? entryName = ReadString(entryObject, "name");

            TaskView view;
            try
            {
                view = entryObject.Deserialize<TaskView>(ViewJson.Options) ?? throw new JsonException("Deserialised to null.");
            }
            catch (JsonException ex)
            {
                invalidViews.Add(new InvalidView(entryId, entryName, [ex.Message]));
                continue;
            }

            IReadOnlyList<string> problems = ViewValidator.Validate(view, fileViews);
            if (problems.Count > 0)
            {
                invalidViews.Add(new InvalidView(entryId, entryName, problems));
                continue;
            }

            fileViews.Add(view);
        }

        List<TaskView> views = [BuiltInOrOverride(fileViews, AllTasksId, DefaultAllTasks), BuiltInOrOverride(fileViews, MyTasksId, DefaultMyTasks)];
        foreach (TaskView view in fileViews)
        {
            if (!IsBuiltIn(view.Id))
            {
                views.Add(view);
            }
        }

        return new Snapshot(views, invalidViews, null);
    }

    /// <summary>The file's own entry for <paramref name="id"/>, marked <see cref="TaskView.BuiltIn"/>, or <paramref name="defaultFactory"/>'s result when the file carries none.</summary>
    private static TaskView BuiltInOrOverride(IReadOnlyList<TaskView> fileViews, string id, Func<TaskView> defaultFactory)
    {
        foreach (TaskView view in fileViews)
        {
            if (string.Equals(view.Id, id, StringComparison.Ordinal))
            {
                return view with { BuiltIn = true };
            }
        }

        return defaultFactory();
    }

    /// <summary>The default, in-memory <c>All Tasks</c> View: every open and closed Task, no filter.</summary>
    private static TaskView DefaultAllTasks() => new()
    {
        Id = AllTasksId,
        Name = "All Tasks",
        Kind = ViewKind.List,
        BuiltIn = true,
    };

    /// <summary>The default, in-memory <c>My Tasks</c> View: the signed-in Human's own Tasks.</summary>
    private static TaskView DefaultMyTasks() => new()
    {
        Id = MyTasksId,
        Name = "My Tasks",
        Kind = ViewKind.List,
        Filter = new TaskFilter { Assignees = ["@me"] },
        BuiltIn = true,
    };

    /// <summary>Reads one string-valued property from <paramref name="entry"/>, or <see langword="null"/> when it is absent, JSON <see langword="null"/>, or not a string.</summary>
    private static string? ReadString(JsonObject entry, string key)
    {
        return entry.TryGetPropertyValue(key, out JsonNode? node) && node is JsonValue value && value.TryGetValue(out string? text)
            ? text
            : null;
    }

    /// <summary>Builds a <see cref="ViewLoadError"/> from <paramref name="ex"/>, converting its 0-based <see cref="JsonException.LineNumber"/> and <see cref="JsonException.BytePositionInLine"/> to 1-based.</summary>
    private static ViewLoadError ToLoadError(string path, JsonException ex)
    {
        long? line = ex.LineNumber is { } lineNumber ? lineNumber + 1 : null;
        long? column = ex.BytePositionInLine is { } bytePosition ? bytePosition + 1 : null;
        return new ViewLoadError($"'{path}' could not be parsed: {ex.Message}", line, column);
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
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

    // See AvatarStore.OnWatcherError's remarks: there is no way to know which change was dropped,
    // so the only correct response is the same one a normal change takes.
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        this.logger.LogWarning(
            e.GetException(),
            "ViewStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.");

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
        Action? changed;
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            Snapshot? rebuilt = this.ReadForWatcherRebuild();
            if (rebuilt is null)
            {
                return;
            }

            // Rebuilt under the same lock, and BEFORE the delegate capture below - see
            // AvatarStore.OnDebounceElapsed's remarks for why this ordering matters.
            this.snapshot = rebuilt;
            changed = this.ViewsChanged;
        }

        changed?.Invoke();
    }

    /// <summary>
    /// Rebuilds the resolved snapshot for an external edit, once the watcher's debounce settles.
    /// Retries a few times first (see <see cref="WatcherReadRetryAttempts"/>) so an in-place write
    /// caught mid-save does not flicker into a spurious <see cref="LoadError"/>; if every attempt
    /// still fails to parse, sets <see cref="LoadError"/> and keeps the previously resolved Views -
    /// see the class remarks' first difference from <see cref="Agency.Huddle.App.Avatars.AvatarStore"/>.
    /// </summary>
    private Snapshot? ReadForWatcherRebuild()
    {
        for (int attempt = 1; attempt <= WatcherReadRetryAttempts; attempt++)
        {
            if (!File.Exists(this.path))
            {
                return BuildSnapshot([]);
            }

            try
            {
                string json = File.ReadAllText(this.path);
                JsonNode? root = JsonNode.Parse(json);
                JsonArray entries = root is JsonObject topLevel && topLevel.TryGetPropertyValue("views", out JsonNode? viewsNode) && viewsNode is JsonArray array
                    ? array
                    : [];
                return BuildSnapshot(entries);
            }
            catch (Exception ex) when (attempt < WatcherReadRetryAttempts && (ex is JsonException or IOException))
            {
                // JsonException: caught mid-write, the partial text does not parse yet.
                // IOException: an editor can hold the file open with a sharing lock while it writes.
                Thread.Sleep(WatcherReadRetryDelayMilliseconds);
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(ex, "Could not parse '{Path}' after a filesystem change, even after retrying; keeping the previously resolved Views.", this.path);
                return Snapshot.WithLoadError(ToLoadError(this.path, ex));
            }
        }

        return null;
    }

    /// <summary>Stops watching <see cref="path"/> and releases the debounce timer.</summary>
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

        this.watcher.EnableRaisingEvents = false;
        this.watcher.Changed -= this.OnWatcherEvent;
        this.watcher.Created -= this.OnWatcherEvent;
        this.watcher.Deleted -= this.OnWatcherEvent;
        this.watcher.Renamed -= this.OnWatcherEvent;
        this.watcher.Error -= this.OnWatcherError;
        this.watcher.Dispose();
        this.debounceTimer.Dispose();
    }

    /// <summary>The result of one attempt to read and parse <c>views.json</c>'s <c>views</c> array.</summary>
    /// <param name="Entries">The parsed array; empty when the file is missing or a parse error occurred.</param>
    /// <param name="LoadError">The parse failure, or <see langword="null"/> on success.</param>
    private sealed record ReadResult(JsonArray Entries, ViewLoadError? LoadError);

    /// <summary>The whole resolved state, swapped in as one immutable reference.</summary>
    /// <param name="Views">The built-ins first, then the file's own Views in file order.</param>
    /// <param name="InvalidViews">Every entry that failed to parse or failed <see cref="ViewValidator"/>.</param>
    /// <param name="LoadError">The current parse failure, or <see langword="null"/>.</param>
    private sealed record Snapshot(IReadOnlyList<TaskView> Views, IReadOnlyList<InvalidView> InvalidViews, ViewLoadError? LoadError)
    {
        /// <summary>A snapshot for a file that failed to parse: no Views, no invalid entries, just the error.</summary>
        public static Snapshot WithLoadError(ViewLoadError loadError) => new([], [], loadError);
    }
}
