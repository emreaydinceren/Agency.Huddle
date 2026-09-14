using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Themes;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Appearance;

/// <summary>
/// Joins the built-in theme pair and <see cref="ThemeCatalog"/> with a Human-editable override file
/// at <c>{DataDir}/appearance.json</c>, resolving the currently selected theme id and the validated
/// override CSS body every render reads through <see cref="Current"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately <see cref="Hooks.HookStore"/>'s sibling, at about a third of the size.</b> Same
/// state model: the resolved snapshot is one immutable <see cref="AppearanceSettings"/> behind a
/// single <see langword="volatile"/> field, one <see cref="Lock"/> around writes, a debounced
/// <see cref="FileSystemWatcher"/>, and an event raised after both the write and the rebuild —
/// never before, or an observer reading this store from inside its own handler would see stale
/// state. <see cref="Save"/> is the only writer.
/// </para>
/// <para>
/// <b>Deliberately synchronous</b>, for the same reason <see cref="Hooks.HookStore"/> gives: this is
/// read from Razor renders, which cannot await, and the file involved is one small JSON document.
/// </para>
/// <para>
/// <b>Tolerance.</b> A missing file is the normal first-run case, not an error — every field
/// resolves to "nothing selected, nothing overridden" and no file is created just to read from. A
/// malformed file logs a warning naming the path and falls back to <see cref="AppearanceSettings.Empty"/>
/// wholesale, the same tolerance <see cref="Hooks.HookStore"/> gives a bad <c>hooks.json</c>. A
/// <c>theme</c> value that names no <see cref="ThemeCatalog"/> entry is a warning, never a failure —
/// <c>rules.md</c>: "A Model the agent does not advertise is a warning, never a failure." — and the
/// file is left exactly as it was, so re-adding the theme (or importing it, per roadmap item 7)
/// restores the choice with no further edit. An unknown top-level key, or an override key that names
/// no theme token, is kept in the file and never silently deleted; <see cref="ThemeOverrides.Build"/>
/// carries the override side of that rule.
/// </para>
/// </remarks>
internal sealed partial class AppearanceStore : IDisposable
{
    // Same reasoning and the same value as HookStore.WatcherDebounceMilliseconds: an editor's save
    // commonly fires several filesystem events in a burst, so this coalesces a burst into one
    // AppearanceChanged per pause in activity rather than thrashing every observer.
    private const int WatcherDebounceMilliseconds = 500;

    // Same reasoning as HookStore.WatcherInternalBufferSize: FileSystemWatcher drops events with no
    // exception and no log when its kernel buffer overflows, raising Error instead - see
    // OnWatcherError. This file is a single small document, so an overflow here is rarer still, but
    // the fix costs nothing.
    private const int WatcherInternalBufferSize = 64 * 1024;

    // Same reasoning as HookStore.WatcherReadRetryAttempts/Delay: a watcher event can fire while a
    // human's editor is still mid-write, so a rebuild retries a few times with a short pause before
    // giving up and keeping the previous snapshot, rather than flickering every token back to its
    // theme default for the width of a save.
    private const int WatcherReadRetryAttempts = 3;
    private const int WatcherReadRetryDelayMilliseconds = 20;

    // See HookStore.IndentedJsonOptions's remarks for why this derives from ProtocolJson.Options
    // rather than using it directly: that instance has no Encoder set, so it inherits
    // JavaScriptEncoder.Default, which escapes every quote and em-dash as a \uXXXX sequence - exactly
    // wrong for a file a Human hand-edits and is likely to fill with "Segoe UI" and similar
    // (traps.md: "ProtocolJson.Options escapes anything unsafe for HTML, which ruins a file a human
    // edits."). WriteIndented makes the file readable in the first place.
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string path;
    private readonly ILogger<AppearanceStore> logger;
    private readonly Lock writeGate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private bool disposed;

    // The whole resolved state, swapped in as one immutable reference. Readers never take a lock -
    // see the class remarks' state-model paragraph.
    private volatile AppearanceSettings current;

    /// <summary>Loads (or defaults) the override file at <c>{DataDir}/appearance.json</c>.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>, already absolutised by <c>ServiceCollectionExtensions</c>'s <c>PostConfigure</c>.</param>
    /// <param name="logger">Used to warn when the override file exists but fails to parse, or names an unknown theme.</param>
    public AppearanceStore(IOptions<TeamOptions> options, ILogger<AppearanceStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.path = Path.Combine(options.Value.DataDir, "appearance.json");

        var directory = Path.GetDirectoryName(this.path);
        if (string.IsNullOrEmpty(directory))
        {
            // TeamOptions.DataDir is always absolutised before this constructor runs (see HookStore's
            // identical guard), so this only exists so the watcher below always has a real directory.
            throw new InvalidOperationException($"'{this.path}' has no parent directory to watch.");
        }

        Directory.CreateDirectory(directory);

        this.current = this.BuildSettings(this.ReadDocumentFromDisk());

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        this.watcher = new FileSystemWatcher(directory, "appearance.json")
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
    /// Raised after <see cref="Save"/> has written the file and rebuilt the resolved snapshot, and
    /// after an external edit to <see cref="FilePath"/> is picked up by the filesystem watcher and
    /// its debounce settles. Raised outside the write lock, for the same reason
    /// <see cref="Hooks.HookStore.HooksChanged"/> is: a future subscriber that takes a lock of its
    /// own cannot deadlock against a concurrent write or watcher rebuild.
    /// </summary>
    public event Action? AppearanceChanged;

    /// <summary>
    /// The absolute path to the override file, <c>appearance.json</c> under
    /// <see cref="TeamOptions.DataDir"/>, whether or not it currently exists — see the class remarks
    /// on why a missing file is the normal case. Exposed so the Appearance tab (T4.1) can tell a user
    /// exactly where to hand-edit it.
    /// </summary>
    public string FilePath => this.path;

    /// <summary>The current resolved theme id and override CSS. No lock: reads the published <see langword="volatile"/> snapshot.</summary>
    public AppearanceSettings Current => this.current;

    /// <summary>
    /// Selects (or clears) the theme id, under the write lock: re-reads the file so a concurrent
    /// hand-edit to <c>overrides</c> is not lost, sets or removes the <c>theme</c> key while leaving
    /// every other key — including <c>overrides</c> — exactly as found, writes, rebuilds the resolved
    /// snapshot, releases the lock, and only then raises <see cref="AppearanceChanged"/>.
    /// </summary>
    /// <param name="themeId">The theme id to store, or <see langword="null"/> to clear the selection and remove the <c>theme</c> key entirely.</param>
    public void Save(string? themeId)
    {
        lock (this.writeGate)
        {
            var document = this.ReadDocumentFromDisk();

            if (themeId is null)
            {
                document.Remove("theme");
            }
            else
            {
                document["theme"] = themeId;
            }

            this.WriteDocumentToDisk(document);
            this.current = this.BuildSettings(document);
        }

        this.AppearanceChanged?.Invoke();
    }

    /// <summary>A valid theme id: lowercase letters, digits and hyphens, 1-64 characters. The same shape a filename built from it must be safe to use as (see <c>rules.md</c>'s Name path-traversal reasoning).</summary>
    [GeneratedRegex(@"\A[a-z0-9-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeIdPattern();

    /// <summary>Whether <paramref name="candidate"/> is a well-formed id naming an entry in <see cref="ThemeCatalog.BuiltIn"/>.</summary>
    /// <param name="candidate">The raw <c>theme</c> value read from the file.</param>
    private static bool IsKnownTheme(string candidate)
    {
        if (!ThemeIdPattern().IsMatch(candidate))
        {
            return false;
        }

        foreach (var theme in ThemeCatalog.BuiltIn)
        {
            if (string.Equals(theme.Id, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the resolved <see cref="AppearanceSettings"/> for <paramref name="document"/>,
    /// validating <c>theme</c> against <see cref="ThemeCatalog"/> and <c>overrides</c> through
    /// <see cref="ThemeOverrides.Build"/>, logging one warning per rejection.
    /// </summary>
    /// <param name="document">The parsed <c>appearance.json</c> object, just read from (or about to be written to) disk.</param>
    private AppearanceSettings BuildSettings(JsonObject document)
    {
        List<string> problems = [];
        string? themeId = null;

        if (document.TryGetPropertyValue("theme", out var themeNode) && themeNode is not null)
        {
            var candidate = themeNode is JsonValue themeValue && themeValue.TryGetValue<string>(out var themeText)
                ? themeText
                : null;

            if (candidate is not null && IsKnownTheme(candidate))
            {
                themeId = candidate;
            }
            else
            {
                var shown = candidate ?? themeNode.ToJsonString();
                problems.Add($"Theme '{shown}' is not a known theme; the built-in theme is used instead.");
                this.logger.LogWarning(
                    "Appearance file '{Path}' selects theme '{ThemeId}', which is not a known theme; the built-in theme is used instead and the file is left unchanged.",
                    this.path,
                    shown);
            }
        }

        Dictionary<string, string> overrides = new(StringComparer.Ordinal);
        if (document.TryGetPropertyValue("overrides", out var overridesNode) && overridesNode is JsonObject overridesObject)
        {
            foreach (var property in overridesObject)
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    overrides[property.Key] = text;
                }
            }
        }

        var overrideCss = ThemeOverrides.Build(overrides, out var overrideProblems);
        foreach (var problem in overrideProblems)
        {
            this.logger.LogWarning(
                "Appearance file '{Path}' rejected an override: {Problem}",
                this.path,
                problem);
        }

        problems.AddRange(overrideProblems);

        return new AppearanceSettings(themeId, overrideCss, problems);
    }

    /// <summary>
    /// Reads the raw override file from disk into a mutable <see cref="JsonObject"/>, tolerating
    /// everything short of a locked file: a missing file yields an empty object (the normal
    /// first-run case, so this never creates the file just to read it), and malformed JSON — or a
    /// top-level value that is not a JSON object — logs a warning naming <see cref="path"/> and also
    /// yields an empty object, falling back to <see cref="AppearanceSettings.Empty"/> wholesale
    /// rather than throwing and preventing startup.
    /// </summary>
    private JsonObject ReadDocumentFromDisk()
    {
        if (!File.Exists(this.path))
        {
            return new JsonObject();
        }

        try
        {
            return ParseDocumentFile(this.path);
        }
        catch (JsonException ex)
        {
            this.logger.LogWarning(ex, "Could not parse appearance file '{Path}'; falling back to no theme and no overrides.", this.path);
            return new JsonObject();
        }
    }

    /// <summary>Serialises <paramref name="document"/> to <see cref="path"/> as indented, human-editable JSON.</summary>
    /// <param name="document">The document to write, unknown top-level keys and all - see the class remarks.</param>
    private void WriteDocumentToDisk(JsonObject document)
    {
        var json = document.ToJsonString(IndentedJsonOptions);
        File.WriteAllText(this.path, json);
    }

    /// <summary>
    /// Reads and parses <paramref name="path"/>'s current contents, throwing <see cref="JsonException"/>
    /// on malformed JSON or a non-object top level - the two callers each react to a parse failure
    /// differently, so the parsing itself carries no fallback policy of its own. Mirrors
    /// <c>HookStore.ParseOverridesFile</c>.
    /// </summary>
    /// <param name="path">The override file's path. Always exists; callers check <see cref="File.Exists(string)"/> first.</param>
    private static JsonObject ParseDocumentFile(string path)
    {
        var json = File.ReadAllText(path);
        var node = JsonNode.Parse(json);
        return node as JsonObject ?? throw new JsonException($"Expected a JSON object at the top level of '{path}'.");
    }

    /// <summary>
    /// Rebuilds the resolved snapshot for an external edit to <see cref="path"/>, once the watcher's
    /// debounce settles. Retries a few times first (see <see cref="WatcherReadRetryAttempts"/>) so an
    /// in-place write caught mid-save does not flicker every override back to the theme default; if
    /// every attempt still fails to parse, returns <see langword="null"/> so the caller
    /// (<see cref="OnDebounceElapsed"/>) keeps the previous resolved snapshot untouched. Mirrors
    /// <c>HookStore.ReadOverridesForWatcherRebuild</c>'s reasoning exactly.
    /// </summary>
    /// <returns>
    /// The freshly parsed document; an empty object if <see cref="path"/> no longer exists (a
    /// legitimate delete); or <see langword="null"/> if every parse attempt failed.
    /// </returns>
    private JsonObject? ReadDocumentForWatcherRebuild()
    {
        if (!File.Exists(this.path))
        {
            return new JsonObject();
        }

        for (var attempt = 1; attempt <= WatcherReadRetryAttempts; attempt++)
        {
            try
            {
                return ParseDocumentFile(this.path);
            }
            catch (Exception ex) when (attempt < WatcherReadRetryAttempts && (ex is JsonException or IOException))
            {
                // JsonException: caught mid-write, the partial text does not parse yet.
                // IOException: an editor can hold the file open with a sharing lock while it writes.
                Thread.Sleep(WatcherReadRetryDelayMilliseconds);
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Could not parse appearance file '{Path}' after a filesystem change, even after retrying; keeping the previously resolved appearance rather than reverting to no theme and no overrides over what may be a mid-write race.",
                    this.path);
                return null;
            }
        }

        return null;
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

    // See HookStore.OnWatcherError's remarks: there is no way to know which change was dropped, so
    // the only correct response is the same one a normal change takes.
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        this.logger.LogWarning(
            e.GetException(),
            "AppearanceStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.");

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

            var document = this.ReadDocumentForWatcherRebuild();
            if (document is null)
            {
                return;
            }

            // Rebuilt under the same lock, and BEFORE the delegate capture below - see
            // HookStore.OnDebounceElapsed's remarks for why this ordering matters.
            this.current = this.BuildSettings(document);
            changed = this.AppearanceChanged;
        }

        changed?.Invoke();
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
}
