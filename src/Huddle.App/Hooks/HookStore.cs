using System.Collections.Frozen;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Hooks;

/// <summary>
/// Joins <see cref="HookCatalog"/>'s built-in defaults with a user-editable override file at
/// <c>{DataDir}/hooks.json</c>, resolving each hook's current text one key at a time. This is the
/// only place a configured override and a catalog default are combined; every reader — a Razor
/// render, an <see cref="Agency.Huddle.Acp.Abstractions.IAppTool.Description"/> getter — goes through
/// <see cref="IHookSource"/> and never reads the file itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>State model.</b> Copies <see cref="Agency.Huddle.App.Acp.PersonaStore"/>'s approach: the
/// resolved overrides are held as one immutable <see cref="FrozenDictionary{TKey, TValue}"/>
/// snapshot behind a single <see langword="volatile"/> field — its immutability is compiler-enforced,
/// unlike a plain <see cref="Dictionary{TKey, TValue}"/>, which would compile just as cleanly with a
/// stray in-place mutation that silently broke the read side's lock-free assumption. Readers take no lock — a
/// <see langword="volatile"/> field is all a single-writer/many-readers publish of an immutable
/// object needs. <see cref="Save"/> and <see cref="Reset"/> are the only writers, and both follow
/// the same order <see cref="Agency.Huddle.App.Acp.PersonaStore.OnDebounceElapsed"/> documents:
/// write the file, rebuild the snapshot, THEN raise <see cref="HooksChanged"/> — never the other
/// order, or an observer that reads this store from inside its own handler would see stale state.
/// </para>
/// <para>
/// <b>Deliberately synchronous</b>, for the same reason <see cref="Agency.Huddle.App.Data.PersonaModelStore"/>
/// gives: this is read from Razor renders and from <see cref="Agency.Huddle.Acp.Abstractions.IAppTool.Description"/>
/// getters, neither of which can await, and the file I/O involved is a single small JSON document.
/// </para>
/// <para>
/// <b>Tolerance.</b> A missing file is the normal first-run case, not an error: every key simply
/// resolves to its catalog default, and no file is created just to read from. A malformed file logs a
/// warning naming the path and falls back to defaults wholesale — the same tolerance
/// <see cref="Agency.Huddle.App.Data.FileChatStore"/> gives a bad JSONL line, applied to a whole
/// document instead of one line, because a hand-edited JSON file can be left mid-edit and the app must
/// still start. A key present in the file but absent from <see cref="HookCatalog"/> is kept — never
/// silently deleted from a user's file — but ignored for resolution.
/// </para>
/// </remarks>
internal sealed class HookStore : IHookSource, IDisposable
{
    // Editors commonly fire several filesystem events per save (a temp-file write plus a rename,
    // or several partial writes), so raising HooksChanged straight off FileSystemWatcher would
    // thrash every observer. Coalesce a burst of events into one HooksChanged per pause in
    // activity - the same reasoning and the same value PersonaStore's WatcherDebounceMilliseconds
    // documents.
    private const int WatcherDebounceMilliseconds = 500;

    // FileSystemWatcher buffers events in a fixed-size kernel window (8 KB by default) and the OS
    // drops events outright - no exception, no log, nothing - when it overflows; it raises the
    // Error event instead. See PersonaStore.WatcherInternalBufferSize's remarks; this file is a
    // single small document rather than a whole directory tree, so an overflow here is rarer
    // still, but the fix costs nothing and OnWatcherError is the real backstop either way.
    private const int WatcherInternalBufferSize = 64 * 1024;

    // A watcher event can fire while an editor is still mid-write (a partial line, an unterminated
    // object). Retrying a few times with a short pause lets a same-process, in-place write settle
    // before this gives up - most editors finish writing well within this window. This is deliberately
    // separate from the constructor's ReadOverridesFromDisk, whose failure has no earlier snapshot to
    // fall back to and so must default; see ReadOverridesForWatcherRebuild's remarks for why a
    // watcher-triggered failure is handled differently.
    private const int WatcherReadRetryAttempts = 3;
    private const int WatcherReadRetryDelayMilliseconds = 20;

    // ProtocolJson.Options writes compact (non-indented) JSON, appropriate for the line-delimited wire
    // protocol it was built for. hooks.json is a file a human is expected to hand-edit, so writing
    // through it here (rather than an unrelated fresh JsonSerializerOptions) keeps every other
    // setting - camelCase, case-insensitive reads - in step with the rest of the app while adding only
    // the indentation this file specifically needs.
    //
    // ProtocolJson.Options also sets no Encoder, so it inherits JavaScriptEncoder.Default, which escapes
    // anything unsafe to drop into HTML or script - every em-dash, apostrophe, quote and angle bracket
    // becomes a \uXXXX sequence. That is the right call for wire JSON that might be interpolated
    // somewhere unknown, and the wrong one here: this file is written to disk and read back by this
    // same serializer, never embedded in HTML or script, so JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    // is not unsafe in this use - "unsafe" names the HTML/script-injection risk this encoder accepts,
    // and no such context exists for a file that only ever round-trips through JsonSerializer. The
    // reason to choose it is simply that a human hand-edits this file, and escaped em-dashes and angle
    // brackets make it unreadable - worse, a user who types a literal "<name>" into hooks.json would see
    // the app rewrite it as "\u003Cname\u003E" on the very next save, which reads as corruption rather
    // than as a JSON encoding detail.
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string path;
    private readonly ILogger<HookStore> logger;
    private readonly Lock writeGate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private bool disposed;

    /// <summary>
    /// The absolute path to the override file, <c>hooks.json</c> under <see cref="TeamOptions.DataDir"/>,
    /// whether or not it currently exists on disk yet — see the class remarks on why a missing file is
    /// the normal case rather than an error. Exposed so the Settings UI can tell a user exactly where
    /// to hand-edit it, and that it is created only on first save.
    /// </summary>
    public string FilePath => this.path;

    // The whole resolved state - one text per catalog key - swapped in as one immutable reference.
    // FrozenDictionary rather than Dictionary: this snapshot is read on every tool Description getter
    // and every turn render but written almost never, exactly the read-many/write-rarely shape
    // FrozenDictionary is optimised for, and its immutability is compiler-enforced rather than merely
    // conventional - there is no indexer setter or Add to accidentally call on the published snapshot.
    // Readers never take a lock: see the state-model remarks above.
    private volatile FrozenDictionary<string, string> resolved;

    /// <summary>Loads (or defaults) the override file at <c>{DataDir}/hooks.json</c> and resolves every catalog key.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>, already absolutised by <c>ServiceCollectionExtensions</c>'s <c>PostConfigure</c>.</param>
    /// <param name="logger">Used to warn when the override file exists but fails to parse.</param>
    public HookStore(IOptions<TeamOptions> options, ILogger<HookStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.path = Path.Combine(options.Value.DataDir, "hooks.json");

        var directory = Path.GetDirectoryName(this.path);
        if (string.IsNullOrEmpty(directory))
        {
            // TeamOptions.DataDir is always absolutised by ServiceCollectionExtensions's
            // PostConfigure before this constructor runs, so Path.Combine(dataDir, "hooks.json")
            // always has a non-empty parent directory in practice. This guard only exists so the
            // watcher below always has a real directory to construct against.
            throw new InvalidOperationException($"'{this.path}' has no parent directory to watch.");
        }

        Directory.CreateDirectory(directory);

        this.resolved = this.ApplyResolved(this.ReadOverridesFromDisk());

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        // FileSystemWatcher cannot watch a single file directly - it watches a directory and
        // matches Filter against the names inside it. A rename-over save (a temp file written
        // then File.Move'd/File.Replace'd into place - common in editors and the exact idiom
        // WriteOverridesToDisk itself could adopt later) arrives as a Renamed event whose Filter
        // match is against the NEW name, which is "hooks.json" again, so the plain Filter below
        // is enough to catch it without PersonaStore's extra directory-rename logic - there are no
        // subdirectories here to worry about.
        this.watcher = new FileSystemWatcher(directory, "hooks.json")
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
    /// Raised after <see cref="Save"/> or <see cref="Reset"/> has written the file and rebuilt the
    /// resolved snapshot, never before, and after an external edit to <see cref="path"/> is picked
    /// up by the filesystem watcher and its debounce settles. Raised outside <see cref="writeGate"/>
    /// so a future subscriber that takes a lock of its own cannot deadlock against a concurrent
    /// write or watcher rebuild.
    /// </summary>
    /// <remarks>
    /// An external edit made while <see cref="Save"/>/<see cref="SaveMany"/>/<see cref="Reset"/> is
    /// also writing will make the watcher fire a second time roughly
    /// <see cref="WatcherDebounceMilliseconds"/> later, raising this event again for what looks
    /// like the same change. That second raise is deliberately NOT suppressed: a rebuild is
    /// idempotent (it reads the same bytes just written) and the Settings page rebuilds its view
    /// from this store plus its own pending edits, so the redundant raise costs nothing. A
    /// "was that my own write?" flag would instead risk swallowing a genuine external edit that
    /// happens to land inside the suppression window - a much worse failure than one extra,
    /// harmless notification.
    /// </remarks>
    public event Action? HooksChanged;

    /// <summary>The hook's current text (override or catalog default) with placeholders substituted.</summary>
    /// <param name="key">The hook's <see cref="HookDefinition.Key"/>.</param>
    /// <param name="values">A value for each placeholder the hook's text may contain.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no hook in <see cref="HookCatalog"/>.</exception>
    public string Render(string key, IReadOnlyDictionary<string, string> values) =>
        HookRenderer.Render(this.Raw(key), values);

    /// <summary>The hook's current text (override or catalog default), unsubstituted.</summary>
    /// <param name="key">The hook's <see cref="HookDefinition.Key"/>.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no hook in <see cref="HookCatalog"/>.</exception>
    public string Raw(string key)
    {
        // HookCatalog.Get throws KeyNotFoundException itself for an unknown key, which is exactly the
        // behaviour this method documents: a caller passing a key that names no hook at all is a
        // programming error, not user data.
        var definition = HookCatalog.Get(key);

        return this.resolved.TryGetValue(key, out var text) ? text : definition.Default;
    }

    /// <summary>
    /// Stores an override for <paramref name="key"/> and writes the file, rebuilding the resolved
    /// snapshot before raising <see cref="HooksChanged"/>. Text equal to the catalog default removes
    /// any existing override instead of storing a redundant copy — the same reasoning
    /// <see cref="Agency.Huddle.App.Data.PersonaModelStore.Set"/> gives for deleting a row instead of
    /// storing a blank.
    /// </summary>
    /// <param name="key">The hook's <see cref="HookDefinition.Key"/>.</param>
    /// <param name="text">The override text to store.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no hook in <see cref="HookCatalog"/>.</exception>
    public void Save(string key, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var definition = HookCatalog.Get(key);

        lock (this.writeGate)
        {
            var overrides = this.ReadOverridesFromDisk();
            ApplyEdit(overrides, definition, text);
            this.WriteOverridesToDisk(overrides);
            this.resolved = this.ApplyResolved(overrides);
        }

        this.HooksChanged?.Invoke();
    }

    /// <summary>
    /// Applies several edits as one save: one re-read of the override file, one write, one snapshot
    /// rebuild, and — the reason this exists rather than a caller looping over <see cref="Save"/> —
    /// exactly one <see cref="HooksChanged"/> event, however many keys <paramref name="edits"/>
    /// carries. Saving five edited fields through five calls to <see cref="Save"/> would write the
    /// file five times and raise five events, which is exactly the failure
    /// <see cref="Agency.Huddle.App.Acp.PersonaStore"/>'s remarks warn against: three writes raising
    /// three events would restart a listening Persona three times for one logical save. Each entry
    /// follows the same default-removes-the-override rule <see cref="Save"/> documents.
    /// </summary>
    /// <param name="edits">
    /// The text to store for each edited key, keyed by <see cref="HookDefinition.Key"/>. An empty
    /// dictionary is a no-op: no file write and no <see cref="HooksChanged"/>.
    /// </param>
    /// <exception cref="KeyNotFoundException">A key in <paramref name="edits"/> names no hook in <see cref="HookCatalog"/>.</exception>
    public void SaveMany(IReadOnlyDictionary<string, string> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return;
        }

        lock (this.writeGate)
        {
            var overrides = this.ReadOverridesFromDisk();

            foreach (var (key, text) in edits)
            {
                var definition = HookCatalog.Get(key);
                ApplyEdit(overrides, definition, text);
            }

            this.WriteOverridesToDisk(overrides);
            this.resolved = this.ApplyResolved(overrides);
        }

        this.HooksChanged?.Invoke();
    }

    /// <summary>
    /// Stores or removes one key's override in an in-memory copy of the file's contents, following
    /// <see cref="Save"/>'s rule: text equal to <paramref name="definition"/>'s catalog default
    /// removes any existing override rather than storing a redundant copy. Shared by <see cref="Save"/>
    /// and <see cref="SaveMany"/> so the rule is written once. The equality check normalises line
    /// endings first — <see cref="HookDefinition.Default"/> carries whatever line endings
    /// <c>HookCatalog.cs</c> had at compile time, while a saved edit from the browser is always
    /// <c>\n</c> — otherwise a value equal to the default in every way but its line endings would be
    /// stored as a redundant override instead of removed, asymmetrically with
    /// <see cref="Agency.Huddle.App.Components.Settings.HookFieldFactory"/>'s own comparison.
    /// </summary>
    /// <param name="overrides">The mutable overrides dictionary being built up before it is written to disk.</param>
    /// <param name="definition">The hook definition <paramref name="text"/> is being applied against.</param>
    /// <param name="text">The override text to store.</param>
    private static void ApplyEdit(Dictionary<string, string> overrides, HookDefinition definition, string text)
    {
        if (string.Equals(text.ReplaceLineEndings("\n"), definition.Default.ReplaceLineEndings("\n"), StringComparison.Ordinal))
        {
            overrides.Remove(definition.Key);
        }
        else
        {
            overrides[definition.Key] = text;
        }
    }

    /// <summary>
    /// Removes any override for <paramref name="key"/>, reverting it to the catalog default, writing
    /// the file and rebuilding the resolved snapshot before raising <see cref="HooksChanged"/>.
    /// </summary>
    /// <param name="key">The hook's <see cref="HookDefinition.Key"/>.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no hook in <see cref="HookCatalog"/>.</exception>
    public void Reset(string key)
    {
        HookCatalog.Get(key);

        lock (this.writeGate)
        {
            var overrides = this.ReadOverridesFromDisk();
            overrides.Remove(key);

            this.WriteOverridesToDisk(overrides);
            this.resolved = this.ApplyResolved(overrides);
        }

        this.HooksChanged?.Invoke();
    }

    /// <summary>
    /// Builds the resolved snapshot for <paramref name="overrides"/> and logs once, at
    /// <see cref="LogLevel.Information"/>, for each key <paramref name="overrides"/> carries that
    /// names no hook in <see cref="HookCatalog"/> — kept in the file (see <see cref="Save"/> and
    /// <see cref="Reset"/>, neither of which deletes an unknown key) but never resolved.
    /// </summary>
    /// <param name="overrides">The overrides just read from (or about to be written to) disk.</param>
    /// <returns>One resolved text per <see cref="HookCatalog"/> key.</returns>
    private FrozenDictionary<string, string> ApplyResolved(Dictionary<string, string> overrides)
    {
        if (this.logger.IsEnabled(LogLevel.Information))
        {
            foreach (var key in overrides.Keys)
            {
                if (!IsKnownHookKey(key))
                {
                    this.logger.LogInformation(
                        "Hook overrides file '{Path}' contains key '{Key}', which is not a hook this application knows about; it is kept in the file but ignored when resolving hook text.",
                        this.path,
                        key);
                }
            }
        }

        return BuildResolved(overrides);
    }

    /// <summary>Whether <paramref name="key"/> names a hook in <see cref="HookCatalog"/>.</summary>
    /// <param name="key">The key to check.</param>
    private static bool IsKnownHookKey(string key)
    {
        foreach (var hook in HookCatalog.All)
        {
            if (string.Equals(hook.Key, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the fully resolved key-to-text map: every <see cref="HookCatalog"/> key, taking
    /// <paramref name="overrides"/>'s value when one exists and the catalog default otherwise. A key
    /// present in <paramref name="overrides"/> but absent from the catalog contributes nothing here —
    /// it is kept in the file (see <see cref="ReadOverridesFromDisk"/> and <see cref="Save"/>, which
    /// never delete an unknown key) but ignored for resolution.
    /// </summary>
    /// <param name="overrides">The overrides read from (or about to be written to) disk.</param>
    /// <returns>One resolved text per <see cref="HookCatalog"/> key.</returns>
    private static FrozenDictionary<string, string> BuildResolved(Dictionary<string, string> overrides)
    {
        var result = new Dictionary<string, string>(HookCatalog.All.Count, StringComparer.Ordinal);

        foreach (var hook in HookCatalog.All)
        {
            result[hook.Key] = overrides.TryGetValue(hook.Key, out var text) ? text : hook.Default;
        }

        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads the raw override file from disk into a mutable dictionary, tolerating everything short of
    /// a locked file: a missing file yields an empty dictionary (the normal first-run case, so this
    /// never creates the file just to read it), and malformed JSON logs a warning naming
    /// <see cref="path"/> and also yields an empty dictionary, falling back to catalog defaults
    /// wholesale rather than throwing and preventing startup.
    /// </summary>
    /// <returns>A mutable copy of whatever overrides the file currently holds, keyed by <see cref="HookDefinition.Key"/> (or an unknown key — see the class remarks).</returns>
    private Dictionary<string, string> ReadOverridesFromDisk()
    {
        if (!File.Exists(this.path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return ParseOverridesFile(this.path);
        }
        catch (JsonException ex)
        {
            this.logger.LogWarning(ex, "Could not parse hook overrides file '{Path}'; falling back to defaults for every hook.", this.path);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>Serialises <paramref name="overrides"/> to <see cref="path"/> as indented, human-editable JSON.</summary>
    /// <param name="overrides">The overrides to write, keyed by <see cref="HookDefinition.Key"/> (an unknown key included, so a user's data is never dropped).</param>
    private void WriteOverridesToDisk(IReadOnlyDictionary<string, string> overrides)
    {
        var json = JsonSerializer.Serialize(overrides, IndentedJsonOptions);
        File.WriteAllText(this.path, json);
    }

    /// <summary>
    /// Reads and parses <paramref name="path"/>'s current contents, throwing <see cref="JsonException"/>
    /// on malformed JSON rather than swallowing it - the two callers (<see cref="ReadOverridesFromDisk"/>
    /// and <see cref="ReadOverridesForWatcherRebuild"/>) each need to react to a parse failure
    /// differently, so the parsing itself carries no fallback policy of its own.
    /// </summary>
    /// <param name="path">The override file's path. Always exists; callers check <see cref="File.Exists(string)"/> first.</param>
    private static Dictionary<string, string> ParseOverridesFile(string path)
    {
        var json = File.ReadAllText(path);
        var overrides = JsonSerializer.Deserialize<Dictionary<string, string>>(json, ProtocolJson.Options);
        return overrides is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(overrides, StringComparer.Ordinal);
    }

    /// <summary>
    /// Rebuilds the resolved snapshot for an external edit to <see cref="path"/>, once the watcher's
    /// debounce settles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a watcher-triggered parse failure is handled differently from a construction-time one.</b>
    /// <see cref="ReadOverridesFromDisk"/> (used by the constructor, <see cref="Save"/>,
    /// <see cref="SaveMany"/> and <see cref="Reset"/>) falls back to defaults wholesale on malformed
    /// JSON because, at construction, there is no earlier resolved snapshot to prefer instead - and
    /// at a Save/Reset, the write this method just performed is presumed well-formed by construction,
    /// so a parse failure there would mean something else raced the write. A watcher rebuild is a
    /// different situation: a hand-edit is very likely to be caught mid-write (an editor's "write, then
    /// rename" or a direct in-place save can both leave a transiently truncated or unterminated file on
    /// disk for a few milliseconds), and reverting every override to its catalog default for that whole
    /// window - only to flip back once the file finishes writing - would be a visible, spurious flicker
    /// for every observer of <see cref="HooksChanged"/>, not a real edit.
    /// </para>
    /// <para>
    /// So this retries a few times with a short pause first (see <see cref="WatcherReadRetryAttempts"/>),
    /// which is enough for an in-place write to settle in practice. If the file still will not parse
    /// after every attempt, this returns <see langword="null"/> rather than an empty dictionary: the
    /// caller (<see cref="OnDebounceElapsed"/>) then keeps the PREVIOUS resolved snapshot untouched
    /// instead of wiping every override - a genuinely malformed hand-edit is a real possibility, but the
    /// user's last-good overrides staying live until they fix it (or until a later event, such as the
    /// file finally settling, retries this again) is a far better failure mode than an editing session's
    /// worth of intentional overrides vanishing over a save-in-progress race.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The freshly parsed overrides; an empty dictionary if <see cref="path"/> no longer exists
    /// (a legitimate delete, reverting every key to its catalog default); or <see langword="null"/> if
    /// every parse attempt failed, meaning the caller should keep its current snapshot.
    /// </returns>
    private Dictionary<string, string>? ReadOverridesForWatcherRebuild()
    {
        if (!File.Exists(this.path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        for (var attempt = 1; attempt <= WatcherReadRetryAttempts; attempt++)
        {
            try
            {
                return ParseOverridesFile(this.path);
            }
            catch (Exception ex) when (attempt < WatcherReadRetryAttempts && (ex is JsonException or IOException))
            {
                // JsonException: caught mid-write, the partial text does not parse yet.
                // IOException: an editor can hold the file open with a sharing lock while it writes.
                // Both are plausible transient states for a file a human is actively saving; a short
                // pause gives the write time to finish before the next attempt.
                Thread.Sleep(WatcherReadRetryDelayMilliseconds);
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Could not parse hook overrides file '{Path}' after a filesystem change, even after retrying; keeping the previously resolved hook text rather than reverting every hook to its catalog default over what may be a mid-write race.",
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

    // FileSystemWatcher raises this instead of a normal change event when its internal buffer
    // overflows and the OS drops events - see WatcherInternalBufferSize's remarks. There is no way
    // to know which change was dropped, so the only correct response is the same one a normal
    // change takes: schedule a refresh on the existing debounce path rather than trust whatever the
    // watcher's view of the world now is.
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        this.logger.LogWarning(
            e.GetException(),
            "HookStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.");

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
            // Checked and captured under the same lock Dispose() takes, so a Dispose() racing this
            // callback either finishes first (this returns without capturing anything) or this
            // captures the delegate before Dispose() can flip the flag - never both.
            if (this.disposed)
            {
                return;
            }

            var overrides = this.ReadOverridesForWatcherRebuild();
            if (overrides is null)
            {
                // See ReadOverridesForWatcherRebuild's remarks: every parse attempt failed, so the
                // existing resolved snapshot is left exactly as it was and nothing is raised - there
                // is nothing new to tell an observer about.
                return;
            }

            // Rebuilt under the same lock, and BEFORE the delegate capture below: an observer reading
            // this store from inside its own HooksChanged handler must see the state the filesystem
            // change just produced, not whatever was true before it - the same ordering
            // PersonaStore.OnDebounceElapsed documents and depends on.
            this.resolved = this.ApplyResolved(overrides);
            changed = this.HooksChanged;
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
