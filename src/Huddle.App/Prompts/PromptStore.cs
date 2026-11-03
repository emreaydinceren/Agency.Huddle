using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Watching;

namespace Agency.Huddle.App.Prompts;

/// <summary>
/// Joins <see cref="PromptCatalog"/>'s built-in defaults with user-editable override files in the Prompts
/// folder, <c>{DataDir}/{Library.Prompts.Dir}/&lt;key&gt;.md</c>, resolving each prompt's current text one key at
/// a time. This is the only place a configured override and a catalog default are combined; every reader — a
/// Razor render, an <see cref="Agency.Huddle.Acp.Abstractions.IAppTool.Description"/> getter — goes through
/// <see cref="IPromptSource"/> and never reads a file itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>State model.</b> Copies <see cref="Agency.Huddle.App.Acp.PersonaStore"/>'s approach: the resolved
/// overrides are held as one immutable <see cref="FrozenDictionary{TKey, TValue}"/> snapshot behind a single
/// <see langword="volatile"/> field, so readers take no lock. <see cref="Save"/>, <see cref="SaveMany"/> and
/// <see cref="Reset"/> are the writers, and all follow the order <see cref="DebouncedFileWatcher"/>'s remarks
/// document: write the files, rebuild the snapshot, THEN raise <see cref="PromptsChanged"/>.
/// </para>
/// <para>
/// <b>Deliberately synchronous</b>: this is read from Razor renders and from tool <c>Description</c> getters,
/// neither of which can await, and the files are small.
/// </para>
/// <para>
/// <b>The folder.</b> A file counts when its extension is <c>.md</c>, ignoring case; its name without the
/// extension is matched to a prompt key ignoring case, and when two files differ only by case the one named
/// exactly like the key wins. Everything else — <see cref="AtomicFile"/>'s <c>&lt;name&gt;.&lt;guid&gt;.tmp</c>
/// temp files, other extensions, names that are no key — is ignored. Line endings are normalised to
/// <c>\n</c>, a BOM is stripped and trailing whitespace is trimmed on read. <c>prompts.json</c> is no longer
/// read, and nothing migrates it.
/// </para>
/// </remarks>
internal sealed class PromptStore : IPromptSource, IDisposable
{
    private const string Extension = ".md";

    private static readonly FrozenDictionary<string, PromptDefinition> KeysIgnoringCase = BuildKeyLookup();

    private readonly string promptsDir;
    private readonly ILogger<PromptStore> logger;

    /// <summary>
    /// The absolute path of the Prompts folder, <c>{DataDir}/{Library.Prompts.Dir}</c>, created on start. The
    /// Settings UI shows it so a user knows where the per-key override files live.
    /// </summary>
    public string FilePath => this.promptsDir;

    // The whole resolved state - one text per catalog key - swapped in as one immutable reference.
    private readonly WatchedFileStore<FrozenDictionary<string, string>> watched;

    /// <summary>Reads the override files in the Prompts folder and resolves every catalog key.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/> (already absolutised) and the Prompts folder name.</param>
    /// <param name="logger">Used to report unknown or unreadable files.</param>
    public PromptStore(IOptions<TeamOptions> options, ILogger<PromptStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.promptsDir = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Library.Prompts.Dir));
        Directory.CreateDirectory(this.promptsDir);

        // Watch "*" and filter by extension in code: a "*.md" filter is case-sensitive on Linux, and
        // rename-over saves and AtomicFile temp files all arrive as events whose content is then unchanged.
        this.watched = new WatchedFileStore<FrozenDictionary<string, string>>(
            this.promptsDir,
            "*",
            this.ApplyResolved(this.ReadOverridesFromDisk()),
            _ => this.ReadOverridesForWatcherRebuild() is { } overrides ? this.ApplyResolved(overrides) : null,
            (previous, rebuilt) => SameContent(previous, rebuilt) ? null : this.PromptsChanged,
            this.logger,
            nameof(PromptStore));
    }

    /// <summary>
    /// Raised after <see cref="Save"/>, <see cref="SaveMany"/> or <see cref="Reset"/> has written the files and
    /// rebuilt the resolved snapshot, never before, and after an external edit to the folder that changes a
    /// resolved prompt is picked up by the watcher and its debounce settles. Raised outside
    /// <see cref="WatchedFileStore{TSnapshot}.Gate"/> so a subscriber that takes a lock of its own cannot deadlock.
    /// </summary>
    /// <remarks>
    /// The watcher re-reads the store's own writes roughly <see cref="DebouncedFileWatcher.DebounceMilliseconds"/>
    /// after them. That re-read raises nothing: the event is raised only when the rebuilt snapshot differs in
    /// content from the one it replaces.
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
        // PromptCatalog.Get throws KeyNotFoundException itself for an unknown key: a programming error, not user data.
        var definition = PromptCatalog.Get(key);

        return this.watched.Current.TryGetValue(key, out var text) ? text : definition.Default;
    }

    /// <summary>Whether the prompt currently resolves to a configured override rather than its catalog default.</summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <returns><see langword="true"/> when the resolved text differs from the catalog default.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no prompt in <see cref="PromptCatalog"/>.</exception>
    public bool IsOverridden(string key) =>
        !string.Equals(this.Raw(key), PromptCatalog.Get(key).Default, StringComparison.Ordinal);

    /// <summary>
    /// Writes <c>&lt;key&gt;.md</c> for <paramref name="key"/> through <see cref="AtomicFile"/>, rebuilding the
    /// resolved snapshot before raising <see cref="PromptsChanged"/>. Text equal to the catalog default deletes
    /// the file instead of storing a redundant copy.
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
            this.ApplyEditToDisk(definition, text);
            this.Republish();
        }

        this.RaiseChanged();
    }

    /// <summary>
    /// Applies several edits as one save: all files are written under the gate, the snapshot is published once
    /// and exactly one <see cref="PromptsChanged"/> is raised, however many keys <paramref name="edits"/>
    /// carries. Each entry follows <see cref="Save"/>'s default-deletes-the-file rule.
    /// </summary>
    /// <remarks>
    /// Not atomic across keys: every file is replaced atomically, but a crash or an IO failure part-way leaves
    /// the keys already processed written and the rest untouched.
    /// </remarks>
    /// <param name="edits">The text to store for each edited key. An empty dictionary is a no-op: no write and no event.</param>
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
            foreach (var (key, text) in edits)
            {
                this.ApplyEditToDisk(PromptCatalog.Get(key), text);
            }

            this.Republish();
        }

        this.RaiseChanged();
    }

    /// <summary>
    /// Deletes any override file for <paramref name="key"/>, reverting it to the catalog default, and rebuilds
    /// the resolved snapshot before raising <see cref="PromptsChanged"/>.
    /// </summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> names no prompt in <see cref="PromptCatalog"/>.</exception>
    public void Reset(string key)
    {
        var definition = PromptCatalog.Get(key);

        lock (this.watched.Gate)
        {
            this.DeleteFilesFor(definition.Key);
            this.Republish();
        }

        this.RaiseChanged();
    }

    /// <summary>Stops watching the folder and releases the debounce timer.</summary>
    public void Dispose() => this.watched.Dispose();

    private static FrozenDictionary<string, PromptDefinition> BuildKeyLookup()
    {
        Dictionary<string, PromptDefinition> lookup = new(PromptCatalog.All.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var prompt in PromptCatalog.All)
        {
            lookup.TryAdd(prompt.Key, prompt);
        }

        return lookup.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether two snapshots resolve every prompt to the same text.</summary>
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

    /// <summary>Builds the fully resolved key-to-text map: the override where one exists, the catalog default otherwise.</summary>
    /// <param name="overrides">The overrides read from disk, keyed by catalog key.</param>
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

    /// <summary>Whether <paramref name="text"/> equals <paramref name="definition"/>'s default once both have their line endings normalised to <c>\n</c>.</summary>
    /// <param name="definition">The prompt definition whose default is compared.</param>
    /// <param name="text">The stored override text.</param>
    private static bool EqualsDefaultApartFromLineEndings(PromptDefinition definition, string text) =>
        string.Equals(text.ReplaceLineEndings("\n"), definition.Default.ReplaceLineEndings("\n"), StringComparison.Ordinal);

    /// <summary>Reads one override file: BOM stripped by the reader, line endings normalised to <c>\n</c>, trailing whitespace trimmed.</summary>
    /// <param name="path">The file to read.</param>
    private static string ReadOverrideFile(string path) =>
        File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd();

    /// <summary>
    /// Writes <paramref name="text"/> to the key's file, or deletes the key's files when it equals the default
    /// apart from line endings. Runs under the gate.
    /// </summary>
    /// <param name="definition">The prompt being edited.</param>
    /// <param name="text">The override text to store.</param>
    private void ApplyEditToDisk(PromptDefinition definition, string text)
    {
        if (EqualsDefaultApartFromLineEndings(definition, text))
        {
            this.DeleteFilesFor(definition.Key);
        }
        else
        {
            AtomicFile.WriteText(Path.Combine(this.promptsDir, definition.Key + Extension), text);
        }
    }

    /// <summary>Deletes every file in the folder that maps to <paramref name="key"/>, whatever its case.</summary>
    /// <param name="key">The prompt key.</param>
    private void DeleteFilesFor(string key)
    {
        foreach (var (path, matched) in this.EnumerateOverrideFiles())
        {
            if (string.Equals(matched.Key, key, StringComparison.Ordinal))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// Deletes every override file whose text equals its prompt's default apart from line endings (a redundant
    /// copy the resolver ignores anyway, so a write drops it), then re-reads the folder and publishes the new
    /// snapshot. Runs under the gate.
    /// </summary>
    private void Republish()
    {
        foreach (var (path, prompt) in this.EnumerateOverrideFiles())
        {
            if (EqualsDefaultApartFromLineEndings(prompt, ReadOverrideFile(path)))
            {
                File.Delete(path);
            }
        }

        this.watched.Current = this.ApplyResolved(this.ReadOverridesFromDisk());
    }

    /// <summary>Raises <see cref="PromptsChanged"/> with a throwing subscriber isolated.</summary>
    private void RaiseChanged() =>
        EventRaise.Isolated(this.PromptsChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.PromptsChanged)));

    /// <summary>Logs overrides that equal their default apart from line endings, then builds the snapshot.</summary>
    /// <param name="overrides">The overrides just read from disk.</param>
    private FrozenDictionary<string, string> ApplyResolved(Dictionary<string, string> overrides)
    {
        if (this.logger.IsEnabled(LogLevel.Information))
        {
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

    /// <summary>
    /// The folder's override files with the prompt each maps to: extension <c>.md</c> ignoring case, name
    /// matched to a key ignoring case, an exact-case name winning over a differently cased one. Files that map
    /// to no key are logged at Information and left out. Sorted by name, so the result does not depend on the
    /// file system's enumeration order.
    /// </summary>
    private List<(string Path, PromptDefinition Prompt)> EnumerateOverrideFiles()
    {
        List<string> files = [.. Directory.EnumerateFiles(this.promptsDir)];
        files.Sort(StringComparer.Ordinal);

        List<(string Path, PromptDefinition Prompt)> all = [];
        foreach (string file in files)
        {
            if (!string.Equals(Path.GetExtension(file), Extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string stem = Path.GetFileNameWithoutExtension(file);
            if (!KeysIgnoringCase.TryGetValue(stem, out PromptDefinition? prompt))
            {
                if (this.logger.IsEnabled(LogLevel.Information))
                {
                    this.logger.LogInformation(
                        "Prompt overrides file '{Path}' names no prompt this application knows about; it is ignored.",
                        file);
                }

                continue;
            }

            all.Add((file, prompt));
        }

        return all;
    }

    /// <summary>Reads every override file in the folder, one winner per key; throws on an IO failure.</summary>
    /// <returns>A mutable map from catalog key to the file's cleaned text.</returns>
    private Dictionary<string, string> ReadOverridesFromDisk()
    {
        Dictionary<string, (string Path, bool Exact)> winners = new(StringComparer.Ordinal);
        foreach (var (path, prompt) in this.EnumerateOverrideFiles())
        {
            bool exact = string.Equals(Path.GetFileNameWithoutExtension(path), prompt.Key, StringComparison.Ordinal);
            if (!winners.TryGetValue(prompt.Key, out var existing) || (exact && !existing.Exact))
            {
                winners[prompt.Key] = (path, exact);
            }
        }

        Dictionary<string, string> overrides = new(StringComparer.Ordinal);
        foreach (var (key, winner) in winners)
        {
            overrides[key] = ReadOverrideFile(winner.Path);
        }

        return overrides;
    }

    /// <summary>
    /// Rebuilds the overrides for a settled watcher event. A file that cannot be read on any attempt (an editor
    /// holds it mid-write) makes the whole rebuild return <see langword="null"/>, so the caller keeps the
    /// previous snapshot: a key never drops to its default over a transient read failure.
    /// </summary>
    /// <returns>The freshly read overrides, or <see langword="null"/> when the caller should keep its current snapshot.</returns>
    private Dictionary<string, string>? ReadOverridesForWatcherRebuild() =>
        RetryingFileRead.Run(this.ReadOverridesFromDisk, this.LogGaveUp);

    private Dictionary<string, string>? LogGaveUp(Exception ex)
    {
        this.logger.LogWarning(
            ex,
            "Could not read the prompt overrides in '{Path}' after {Attempts} attempts; keeping the previously resolved prompt text.",
            this.promptsDir,
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; same as RetryingFileRead.
            RetryingFileRead.Attempts);
#pragma warning restore HAA0601
        return null;
    }
}
