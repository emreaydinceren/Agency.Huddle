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
internal sealed class HookStore : IHookSource
{
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
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        this.resolved = this.ApplyResolved(this.ReadOverridesFromDisk());
    }

    /// <summary>
    /// Raised after <see cref="Save"/> or <see cref="Reset"/> has written the file and rebuilt the
    /// resolved snapshot, never before. Raised outside <see cref="writeGate"/> so a future subscriber
    /// that takes a lock of its own cannot deadlock against a concurrent write.
    /// </summary>
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

            if (string.Equals(text, definition.Default, StringComparison.Ordinal))
            {
                overrides.Remove(key);
            }
            else
            {
                overrides[key] = text;
            }

            this.WriteOverridesToDisk(overrides);
            this.resolved = this.ApplyResolved(overrides);
        }

        this.HooksChanged?.Invoke();
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
            var json = File.ReadAllText(this.path);
            var overrides = JsonSerializer.Deserialize<Dictionary<string, string>>(json, ProtocolJson.Options);
            return overrides is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(overrides, StringComparer.Ordinal);
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
}
