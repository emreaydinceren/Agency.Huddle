using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Watching;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Prompts;

/// <summary>
/// Joins <see cref="PromptCatalog"/>'s built-in defaults with a user-editable override file at
/// <c>{DataDir}/prompts.json</c>, resolving each prompt's current text one key at a time. This is the
/// only place a configured override and a catalog default are combined; every reader — a Razor
/// render, an <see cref="Agency.Huddle.Acp.Abstractions.IAppTool.Description"/> getter — goes through
/// <see cref="IPromptSource"/> and never reads the file itself.
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
/// the order <see cref="DebouncedFileWatcher"/>'s remarks document:
/// write the file, rebuild the snapshot, THEN raise <see cref="PromptsChanged"/> — never the other
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
/// still start. A key present in the file but absent from <see cref="PromptCatalog"/> is kept — never
/// silently deleted from a user's file — but ignored for resolution.
/// </para>
/// </remarks>
internal sealed class PromptStore : IPromptSource, IDisposable
{
    private readonly string path;
    private readonly ILogger<PromptStore> logger;

    /// <summary>
    /// The absolute path to the override file, <c>prompts.json</c> under <see cref="TeamOptions.DataDir"/>,
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
    private readonly WatchedFileStore<FrozenDictionary<string, string>> watched;

    /// <summary>Loads (or defaults) the override file at <c>{DataDir}/prompts.json</c> and resolves every catalog key.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>, already absolutised by <c>ServiceCollectionExtensions</c>'s <c>PostConfigure</c>.</param>
    /// <param name="logger">Used to warn when the override file exists but fails to parse.</param>
    public PromptStore(IOptions<TeamOptions> options, ILogger<PromptStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.path = Path.Combine(options.Value.DataDir, "prompts.json");

        var directory = Path.GetDirectoryName(this.path);
        if (string.IsNullOrEmpty(directory))
        {
            // TeamOptions.DataDir is always absolutised by ServiceCollectionExtensions's
            // PostConfigure before this constructor runs, so Path.Combine(dataDir, "prompts.json")
            // always has a non-empty parent directory in practice. This guard only exists so the
            // watcher below always has a real directory to construct against.
            throw new InvalidOperationException($"'{this.path}' has no parent directory to watch.");
        }

        Directory.CreateDirectory(directory);

        // FileSystemWatcher cannot watch a single file directly - it watches a directory and
        // matches Filter against the names inside it. A rename-over save (a temp file written
        // then File.Move'd/File.Replace'd into place - common in editors) arrives as a Renamed
        // event whose Filter match is against the NEW name, which is "prompts.json" again, so the
        // plain filter below is enough to catch it - there are no subdirectories here to worry about.
        this.watched = new WatchedFileStore<FrozenDictionary<string, string>>(
            directory,
            "prompts.json",
            this.ApplyResolved(this.ReadOverridesFromDisk()),
            _ => this.ReadOverridesForWatcherRebuild() is { } overrides ? this.ApplyResolved(overrides) : null,
            (previous, rebuilt) => SameContent(previous, rebuilt) ? null : this.PromptsChanged,
            this.logger,
            nameof(PromptStore));
    }

    /// <summary>
    /// Raised after <see cref="Save"/> or <see cref="Reset"/> has written the file and rebuilt the
    /// resolved snapshot, never before, and after an external edit to <see cref="path"/> that changes a
    /// resolved prompt is picked up by the filesystem watcher and its debounce settles. Raised outside <see cref="WatchedFileStore{TSnapshot}.Gate"/>
    /// so a future subscriber that takes a lock of its own cannot deadlock against a concurrent
    /// write or watcher rebuild.
    /// </summary>
    /// <remarks>
    /// The watcher re-reads <see cref="Save"/>/<see cref="SaveMany"/>/<see cref="Reset"/>'s own write roughly
    /// <see cref="DebouncedFileWatcher.DebounceMilliseconds"/> after it. That re-read raises nothing: the
    /// event is raised only when the rebuilt snapshot differs in content from the one it replaces, so a
    /// re-read of bytes the store just wrote, or a rewrite of the file with the same overrides, is silent.
    /// There is no "was that my own write?" flag, so a genuine external edit that lands inside the
    /// debounce window still differs from the published snapshot and still raises.
    /// </remarks>
    public event Action? PromptsChanged;

    /// <summary>The prompt's current text (override or catalog default) with placeholders substituted.</summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <param name="values">A value for each placeholder the prompt's text may contain.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no prompt in <see cref="PromptCatalog"/>.</exception>
    public string Render(string key, IReadOnlyDictionary<string, string> values) =>
        PromptRenderer.Render(this.Raw(key), values);

    /// <summary>The prompt's current text (override or catalog default), unsubstituted.</summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no prompt in <see cref="PromptCatalog"/>.</exception>
    public string Raw(string key)
    {
        // PromptCatalog.Get throws KeyNotFoundException itself for an unknown key, which is exactly the
        // behaviour this method documents: a caller passing a key that names no prompt at all is a
        // programming error, not user data.
        var definition = PromptCatalog.Get(key);

        return this.watched.Current.TryGetValue(key, out var text) ? text : definition.Default;
    }

    /// <summary>
    /// Stores an override for <paramref name="key"/> and writes the file, rebuilding the resolved
    /// snapshot before raising <see cref="PromptsChanged"/>. Text equal to the catalog default removes
    /// any existing override instead of storing a redundant copy — the same reasoning
    /// <see cref="Agency.Huddle.App.Data.PersonaModelStore.Set"/> gives for deleting a row instead of
    /// storing a blank.
    /// </summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <param name="text">The override text to store.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no prompt in <see cref="PromptCatalog"/>.</exception>
    public void Save(string key, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var definition = PromptCatalog.Get(key);

        lock (this.watched.Gate)
        {
            var overrides = this.ReadOverridesFromDisk();
            ApplyEdit(overrides, definition, text);
            DropIgnoredOverrides(overrides);
            this.WriteOverridesToDisk(overrides);
            this.watched.Current = this.ApplyResolved(overrides);
        }

        this.PromptsChanged?.Invoke();
    }

    /// <summary>
    /// Applies several edits as one save: one re-read of the override file, one write, one snapshot
    /// rebuild, and — the reason this exists rather than a caller looping over <see cref="Save"/> —
    /// exactly one <see cref="PromptsChanged"/> event, however many keys <paramref name="edits"/>
    /// carries. Saving five edited fields through five calls to <see cref="Save"/> would write the
    /// file five times and raise five events, which is exactly the failure
    /// <see cref="Agency.Huddle.App.Acp.PersonaStore"/>'s remarks warn against: three writes raising
    /// three events would restart a listening Persona three times for one logical save. Each entry
    /// follows the same default-removes-the-override rule <see cref="Save"/> documents.
    /// </summary>
    /// <param name="edits">
    /// The text to store for each edited key, keyed by <see cref="PromptDefinition.Key"/>. An empty
    /// dictionary is a no-op: no file write and no <see cref="PromptsChanged"/>.
    /// </param>
    /// <exception cref="KeyNotFoundException">A key in <paramref name="edits"/> names no prompt in <see cref="PromptCatalog"/>.</exception>
    public void SaveMany(IReadOnlyDictionary<string, string> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return;
        }

        lock (this.watched.Gate)
        {
            var overrides = this.ReadOverridesFromDisk();

            foreach (var (key, text) in edits)
            {
                var definition = PromptCatalog.Get(key);
                ApplyEdit(overrides, definition, text);
            }

            DropIgnoredOverrides(overrides);
            this.WriteOverridesToDisk(overrides);
            this.watched.Current = this.ApplyResolved(overrides);
        }

        this.PromptsChanged?.Invoke();
    }

    /// <summary>
    /// Stores or removes one key's override in an in-memory copy of the file's contents, following
    /// <see cref="Save"/>'s rule: text equal to <paramref name="definition"/>'s catalog default
    /// removes any existing override rather than storing a redundant copy. Shared by <see cref="Save"/>
    /// and <see cref="SaveMany"/> so the rule is written once. The equality check normalises line
    /// endings first — <see cref="PromptDefinition.Default"/> carries whatever line endings
    /// <c>PromptCatalog.cs</c> had at compile time, while a saved edit from the browser is always
    /// <c>\n</c> — otherwise a value equal to the default in every way but its line endings would be
    /// stored as a redundant override instead of removed, asymmetrically with
    /// <see cref="Agency.Huddle.App.Components.Settings.PromptFieldFactory"/>'s own comparison.
    /// </summary>
    /// <param name="overrides">The mutable overrides dictionary being built up before it is written to disk.</param>
    /// <param name="definition">The prompt definition <paramref name="text"/> is being applied against.</param>
    /// <param name="text">The override text to store.</param>
    private static void ApplyEdit(Dictionary<string, string> overrides, PromptDefinition definition, string text)
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
    /// the file and rebuilding the resolved snapshot before raising <see cref="PromptsChanged"/>.
    /// </summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no prompt in <see cref="PromptCatalog"/>.</exception>
    public void Reset(string key)
    {
        PromptCatalog.Get(key);

        lock (this.watched.Gate)
        {
            var overrides = this.ReadOverridesFromDisk();
            overrides.Remove(key);
            DropIgnoredOverrides(overrides);

            this.WriteOverridesToDisk(overrides);
            this.watched.Current = this.ApplyResolved(overrides);
        }

        this.PromptsChanged?.Invoke();
    }

    /// <summary>
    /// Builds the resolved snapshot for <paramref name="overrides"/> and logs once, at
    /// <see cref="LogLevel.Information"/>, for each key <paramref name="overrides"/> carries that
    /// names no prompt in <see cref="PromptCatalog"/> — kept in the file (see <see cref="Save"/> and
    /// <see cref="Reset"/>, neither of which deletes an unknown key) but never resolved.
    /// </summary>
    /// <param name="overrides">The overrides just read from (or about to be written to) disk.</param>
    /// <returns>One resolved text per <see cref="PromptCatalog"/> key.</returns>
    private FrozenDictionary<string, string> ApplyResolved(Dictionary<string, string> overrides)
    {
        if (this.logger.IsEnabled(LogLevel.Information))
        {
            foreach (var key in overrides.Keys)
            {
                if (!IsKnownPromptKey(key))
                {
                    this.logger.LogInformation(
                        "Prompt overrides file '{Path}' contains key '{Key}', which is not a prompt this application knows about; it is kept in the file but ignored when resolving prompt text.",
                        this.path,
                        key);
                }
            }

            foreach (var prompt in PromptCatalog.All)
            {
                if (overrides.TryGetValue(prompt.Key, out var text)
                    && !string.Equals(text, prompt.Default, StringComparison.Ordinal)
                    && EqualsDefaultApartFromLineEndings(prompt, text))
                {
                    this.logger.LogInformation(
                        "Prompt override '{Key}' equals the default apart from line endings and is ignored.",
                        prompt.Key);
                }
            }
        }

        return BuildResolved(overrides);
    }

    /// <summary>Whether two snapshots resolve every prompt to the same text: count plus every key and value ordinally, because a <see cref="FrozenDictionary{TKey, TValue}"/> has no value equality of its own.</summary>
    /// <param name="previous">The snapshot being replaced.</param>
    /// <param name="rebuilt">The snapshot the watcher just published.</param>
    private static bool SameContent(FrozenDictionary<string, string> previous, FrozenDictionary<string, string> rebuilt)
    {
        if (previous.Count != rebuilt.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> entry in previous)
        {
            if (!rebuilt.TryGetValue(entry.Key, out string? other) || !string.Equals(entry.Value, other, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether <paramref name="key"/> names a prompt in <see cref="PromptCatalog"/>.</summary>
    /// <param name="key">The key to check.</param>
    private static bool IsKnownPromptKey(string key)
    {
        foreach (var prompt in PromptCatalog.All)
        {
            if (string.Equals(prompt.Key, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the fully resolved key-to-text map: every <see cref="PromptCatalog"/> key, taking
    /// <paramref name="overrides"/>'s value when one exists and the catalog default otherwise. A key
    /// present in <paramref name="overrides"/> but absent from the catalog contributes nothing here —
    /// it is kept in the file (see <see cref="ReadOverridesFromDisk"/> and <see cref="Save"/>, which
    /// never delete an unknown key) but ignored for resolution.
    /// </summary>
    /// <param name="overrides">The overrides read from (or about to be written to) disk.</param>
    /// <returns>One resolved text per <see cref="PromptCatalog"/> key.</returns>
    private static FrozenDictionary<string, string> BuildResolved(Dictionary<string, string> overrides)
    {
        var result = new Dictionary<string, string>(PromptCatalog.All.Count, StringComparer.Ordinal);

        foreach (var prompt in PromptCatalog.All)
        {
            result[prompt.Key] = overrides.TryGetValue(prompt.Key, out var text) && !EqualsDefaultApartFromLineEndings(prompt, text)
                ? text
                : prompt.Default;
        }

        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether <paramref name="text"/> equals <paramref name="definition"/>'s default once both sides
    /// have their line endings normalised to <c>\n</c> by <see cref="string.ReplaceLineEndings(string)"/>,
    /// which also normalises a lone CR, NEL, LS, PS and FF. Such a stored value is a redundant override
    /// and is ignored when resolving, whatever its line endings.
    /// </summary>
    /// <param name="definition">The prompt definition whose default is compared.</param>
    /// <param name="text">The stored override text.</param>
    private static bool EqualsDefaultApartFromLineEndings(PromptDefinition definition, string text) =>
        string.Equals(text.ReplaceLineEndings("\n"), definition.Default.ReplaceLineEndings("\n"), StringComparison.Ordinal);

    /// <summary>
    /// Removes from <paramref name="overrides"/> every value equal to its prompt's default apart from
    /// line endings, so the next write drops what <see cref="BuildResolved"/> already ignores.
    /// </summary>
    /// <param name="overrides">The mutable overrides dictionary about to be written to disk.</param>
    private static void DropIgnoredOverrides(Dictionary<string, string> overrides)
    {
        foreach (var prompt in PromptCatalog.All)
        {
            if (overrides.TryGetValue(prompt.Key, out var text) && EqualsDefaultApartFromLineEndings(prompt, text))
            {
                overrides.Remove(prompt.Key);
            }
        }
    }

    /// <summary>
    /// Reads the raw override file from disk into a mutable dictionary, tolerating everything short of
    /// a locked file: a missing file yields an empty dictionary (the normal first-run case, so this
    /// never creates the file just to read it), and malformed JSON logs a warning naming
    /// <see cref="path"/> and also yields an empty dictionary, falling back to catalog defaults
    /// wholesale rather than throwing and preventing startup.
    /// </summary>
    /// <returns>A mutable copy of whatever overrides the file currently holds, keyed by <see cref="PromptDefinition.Key"/> (or an unknown key — see the class remarks).</returns>
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
            this.logger.LogWarning(ex, "Could not parse prompt overrides file '{Path}'; falling back to defaults for every prompt.", this.path);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>Serialises <paramref name="overrides"/> to <see cref="path"/> as indented, human-editable JSON.</summary>
    /// <param name="overrides">The overrides to write, keyed by <see cref="PromptDefinition.Key"/> (an unknown key included, so a user's data is never dropped).</param>
    private void WriteOverridesToDisk(IReadOnlyDictionary<string, string> overrides)
    {
        var json = JsonSerializer.Serialize(overrides, StoreJson.Indented);
        AtomicFile.WriteText(this.path, json);
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
    /// for every observer of <see cref="PromptsChanged"/>, not a real edit.
    /// </para>
    /// <para>
    /// So this retries a few times with a short pause first (see <see cref="RetryingFileRead.Attempts"/>),
    /// which is enough for an in-place write to settle in practice. If the file still will not parse
    /// after every attempt, this returns <see langword="null"/> rather than an empty dictionary: the
    /// caller (<see cref="WatchedFileStore{TSnapshot}"/>) then keeps the PREVIOUS resolved snapshot untouched
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
    private Dictionary<string, string>? ReadOverridesForWatcherRebuild() =>
        RetryingFileRead.RereadKeepingPrevious(this.path, static () => new Dictionary<string, string>(StringComparer.Ordinal), () => ParseOverridesFile(this.path), this.logger, "prompt overrides file", "prompt text");

    /// <summary>Stops watching <see cref="path"/> and releases the debounce timer.</summary>
    public void Dispose() => this.watched.Dispose();
}
