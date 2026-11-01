using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.App.Watching;

namespace Agency.Huddle.App.Appearance;

/// <summary>
/// Joins <see cref="ThemeCatalog"/> with a Human-editable selection file at
/// <c>{DataDir}/appearance.json</c>, resolving the currently selected theme id every render reads
/// through <see cref="Current"/>. MudBlazor's <see cref="MudBlazor.MudTheme"/> is the single source
/// of theming now, so this store carries no override CSS and no per-token validation — it only
/// remembers which catalog entry the Human picked, and a catalog entry now carries its own light or
/// dark palette, so there is nothing else left to remember
/// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately <see cref="Prompts.PromptStore"/>'s sibling, at about a third of the size.</b> Same
/// state model: the resolved snapshot is one immutable <see cref="AppearanceSettings"/> behind a
/// single <see langword="volatile"/> field, one <see cref="Lock"/> around writes, a debounced
/// <see cref="FileSystemWatcher"/>, and an event raised after both the write and the rebuild —
/// never before, or an observer reading this store from inside its own handler would see stale
/// state. <see cref="Save"/> and <see cref="SaveAccentColor"/> are the only writers, and both share
/// the same read-modify-one-key-write shape so neither can clobber the other's key.
/// </para>
/// <para>
/// <b>Deliberately synchronous</b>, for the same reason <see cref="Prompts.PromptStore"/> gives: this is
/// read from Razor renders, which cannot await, and the file involved is one small JSON document.
/// </para>
/// <para>
/// <b>Tolerance.</b> A missing file is the normal first-run case, not an error — every field
/// resolves to its default (no theme selected, nothing customised) and no file is created just to read from. A
/// malformed file logs a warning naming the path and falls back to those defaults
/// wholesale, the same tolerance <see cref="Prompts.PromptStore"/> gives a bad <c>prompts.json</c>. A
/// <c>theme</c> value that names no <see cref="ThemeCatalog"/> entry is a warning, never a failure —
/// <c>rules.md</c>: "A Model the agent does not advertise is a warning, never a failure." — and the
/// file is left exactly as it was, so fixing the value restores the choice with no further edit. An
/// unknown top-level key is kept in the file and never silently deleted. That last rule is what
/// handles the retired <c>dark</c> key: a file written before 2026-09-21 still carries one, and it
/// is now simply an unknown key — ignored on read, preserved on write, and never warned about.
/// </para>
/// </remarks>
internal sealed partial class AppearanceStore : IDisposable
{
    private readonly string path;
    private readonly ILogger<AppearanceStore> logger;

    // The whole resolved state, swapped in as one immutable reference. Readers never take a lock -
    // see the class remarks' state-model paragraph.
    private readonly WatchedFileStore<AppearanceSettings> watched;

    /// <summary>Loads (or defaults) the selection file at <c>{DataDir}/appearance.json</c>.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>, already absolutised by <c>ServiceCollectionExtensions</c>'s <c>PostConfigure</c>.</param>
    /// <param name="logger">Used to warn when the selection file exists but fails to parse, or names an unknown theme.</param>
    public AppearanceStore(IOptions<TeamOptions> options, ILogger<AppearanceStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.path = Path.Combine(options.Value.DataDir, "appearance.json");

        var directory = Path.GetDirectoryName(this.path);
        if (string.IsNullOrEmpty(directory))
        {
            // TeamOptions.DataDir is always absolutised before this constructor runs (see PromptStore's
            // identical guard), so this only exists so the watcher below always has a real directory.
            throw new InvalidOperationException($"'{this.path}' has no parent directory to watch.");
        }

        Directory.CreateDirectory(directory);

        var document = this.ReadDocumentFromDisk();
        if (this.MigrateRetiredThemes(document))
        {
            // Written once, here, before the watcher exists, so the rewrite raises no event; a second load finds nothing to migrate.
            this.WriteDocumentToDisk(document);
        }

        this.watched = new WatchedFileStore<AppearanceSettings>(
            directory,
            "appearance.json",
            this.BuildSettings(document),
            _ => this.ReadDocumentForWatcherRebuild() is { } rebuilt ? this.BuildSettings(rebuilt) : null,
            (previous, rebuilt) => SameContent(previous, rebuilt) ? null : this.AppearanceChanged,
            this.logger,
            nameof(AppearanceStore));
    }

    /// <summary>
    /// Raised after <see cref="Save"/> has written the file and rebuilt the resolved snapshot, and
    /// after an external edit to <see cref="FilePath"/> that changes the resolved appearance is picked up by
    /// the filesystem watcher and its debounce settles. Raised outside the write lock, for the same reason
    /// <see cref="Prompts.PromptStore.PromptsChanged"/> is: a future subscriber that takes a lock of its
    /// own cannot deadlock against a concurrent write or watcher rebuild. The watcher's re-read of the
    /// store's own write, or a rewrite of the file that resolves to the same appearance, raises nothing.
    /// </summary>
    public event Action? AppearanceChanged;

    /// <summary>
    /// The absolute path to the selection file, <c>appearance.json</c> under
    /// <see cref="TeamOptions.DataDir"/>, whether or not it currently exists — see the class remarks
    /// on why a missing file is the normal case. Exposed so the Appearance tab can tell a user
    /// exactly where the file lives.
    /// </summary>
    public string FilePath => this.path;

    /// <summary>The current resolved theme id. No lock: reads the published <see langword="volatile"/> snapshot.</summary>
    public AppearanceSettings Current => this.watched.Current;

    /// <summary>
    /// Selects (or clears) the theme id, under the write lock: re-reads the file so a concurrent
    /// hand-edit is not lost, sets <c>theme</c> while leaving every other key exactly as found,
    /// writes, rebuilds the resolved snapshot, releases the lock, and only then raises
    /// <see cref="AppearanceChanged"/>.
    /// </summary>
    /// <param name="themeId">The theme id to store, or <see langword="null"/> to clear the selection and remove the <c>theme</c> key entirely.</param>
    public void Save(string? themeId)
    {
        lock (this.watched.Gate)
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
            this.watched.Current = this.BuildSettings(document);
        }

        EventRaise.Isolated(this.AppearanceChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.AppearanceChanged)));
    }

    /// <summary>
    /// Sets (or clears) the Human's own accent colour for one Theme: <c>customizations[themeId].palette.primary</c>, under the
    /// write lock, leaving every other key - the Theme's other customizations included - exactly as found. A value that is
    /// not <c>#rrggbb</c>, or an id that names no Theme, is refused: nothing is written and nothing is raised. Clearing a
    /// colour that is not set is likewise a no-op. An entry left empty by clearing is removed, and so is an empty
    /// <c>customizations</c> object.
    /// </summary>
    /// <param name="themeId">The Theme the accent belongs to.</param>
    /// <param name="accentColorHex">The accent colour to store, as <c>#rrggbb</c>, or <see langword="null"/> to clear it.</param>
    public void SaveAccentColor(string themeId, string? accentColorHex)
    {
        ArgumentNullException.ThrowIfNull(themeId);

        if (FindTheme(themeId) is null || (accentColorHex is not null && !AccentColorPattern().IsMatch(accentColorHex)))
        {
            return;
        }

        this.Mutate(document =>
        {
            if (accentColorHex is null
                && (document["customizations"] is not JsonObject existing
                    || existing[themeId] is not JsonObject entry
                    || entry["palette"] is not JsonObject existingPalette
                    || !existingPalette.ContainsKey("primary")))
            {
                return false;
            }

            JsonObject customizations = EnsureObject(document, "customizations");
            JsonObject themeEntry = EnsureObject(customizations, themeId);
            JsonObject palette = EnsureObject(themeEntry, "palette");

            if (accentColorHex is null)
            {
                _ = palette.Remove("primary");
            }
            else
            {
                palette["primary"] = accentColorHex;
            }

            PruneEmpty(document, customizations, themeId, themeEntry, palette);
            return true;
        });
    }

    /// <summary>
    /// Removes everything the Human customised for one Theme: its whole <c>customizations[themeId]</c> entry, palette and
    /// tokens, under the write lock, leaving every other key as found. A Theme with no entry is a no-op: nothing is written
    /// and nothing is raised.
    /// </summary>
    /// <param name="themeId">The Theme to reset.</param>
    public void ResetCustomization(string themeId)
    {
        ArgumentNullException.ThrowIfNull(themeId);

        this.Mutate(document =>
        {
            if (document["customizations"] is not JsonObject customizations || !customizations.ContainsKey(themeId))
            {
                return false;
            }

            _ = customizations.Remove(themeId);
            if (customizations.Count == 0)
            {
                _ = document.Remove("customizations");
            }

            return true;
        });
    }

    /// <summary>
    /// Runs <paramref name="edit"/> over a fresh read of the file under the write lock; when it reports a change, writes the
    /// file, rebuilds the snapshot, releases the lock, and only then raises <see cref="AppearanceChanged"/> - never before, or
    /// an observer would see stale state.
    /// </summary>
    /// <param name="edit">Edits the document in place and returns whether it changed anything.</param>
    private void Mutate(Func<JsonObject, bool> edit)
    {
        lock (this.watched.Gate)
        {
            var document = this.ReadDocumentFromDisk();
            if (!edit(document))
            {
                return;
            }

            this.WriteDocumentToDisk(document);
            this.watched.Current = this.BuildSettings(document);
        }

        EventRaise.Isolated(this.AppearanceChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.AppearanceChanged)));
    }

    /// <summary>
    /// Whether two snapshots resolve to the same appearance: the three ids by value, and the customizations by count and then
    /// each Theme's <see cref="SkinCustomization.CacheKey"/>, because a record holding dictionaries compares them by reference.
    /// </summary>
    /// <param name="previous">The snapshot being replaced.</param>
    /// <param name="rebuilt">The snapshot the watcher just published.</param>
    private static bool SameContent(AppearanceSettings previous, AppearanceSettings rebuilt)
    {
        if (!string.Equals(previous.ThemeId, rebuilt.ThemeId, StringComparison.Ordinal)
            || !string.Equals(previous.LightThemeId, rebuilt.LightThemeId, StringComparison.Ordinal)
            || !string.Equals(previous.DarkThemeId, rebuilt.DarkThemeId, StringComparison.Ordinal)
            || previous.Customizations.Count != rebuilt.Customizations.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, SkinCustomization> entry in previous.Customizations)
        {
            if (!rebuilt.Customizations.TryGetValue(entry.Key, out SkinCustomization? other)
                || !string.Equals(entry.Value.CacheKey, other.CacheKey, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The object at <paramref name="key"/> of <paramref name="parent"/>, created and attached when it is missing or not an object.</summary>
    /// <param name="parent">The object holding the key.</param>
    /// <param name="key">The key.</param>
    private static JsonObject EnsureObject(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing)
        {
            return existing;
        }

        JsonObject created = new();
        parent[key] = created;
        return created;
    }

    /// <summary>Detaches each emptied level of a customization from the document, innermost first, so clearing the last value leaves no empty shell behind.</summary>
    /// <param name="document">The whole document.</param>
    /// <param name="customizations">Its <c>customizations</c> object.</param>
    /// <param name="themeId">The Theme id keying the entry.</param>
    /// <param name="entry">The Theme's entry.</param>
    /// <param name="palette">The entry's palette object.</param>
    private static void PruneEmpty(JsonObject document, JsonObject customizations, string themeId, JsonObject entry, JsonObject palette)
    {
        if (palette.Count == 0)
        {
            _ = entry.Remove("palette");
        }

        if (entry.Count == 0)
        {
            _ = customizations.Remove(themeId);
        }

        if (customizations.Count == 0)
        {
            _ = document.Remove("customizations");
        }
    }

    /// <summary>
    /// Chooses <paramref name="themeId"/> as the Human's light or dark theme - whichever its
    /// <see cref="ThemeDescriptor.Mode"/> says - under the write lock. The theme being shown changes
    /// too when it is of the same mode (picking a new dark theme while in dark mode repaints at once);
    /// picking the other mode's theme only records the choice for the next toggle.
    /// </summary>
    /// <param name="themeId">A <see cref="ThemeCatalog"/> id; an unknown id is ignored.</param>
    public void SavePreferred(string themeId)
    {
        ArgumentNullException.ThrowIfNull(themeId);

        var chosen = FindTheme(themeId);
        if (chosen is null)
        {
            return;
        }

        lock (this.watched.Gate)
        {
            var document = this.ReadDocumentFromDisk();
            var shownMode = (FindTheme(this.BuildSettings(document).ThemeId) ?? ThemeCatalog.BuiltIn[0]).Mode;

            document[chosen.Mode == ThemeMode.Dark ? "darkTheme" : "lightTheme"] = chosen.Id;
            if (shownMode == chosen.Mode)
            {
                document["theme"] = chosen.Id;
            }

            this.WriteDocumentToDisk(document);
            this.watched.Current = this.BuildSettings(document);
        }

        EventRaise.Isolated(this.AppearanceChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.AppearanceChanged)));
    }

    /// <summary>
    /// Switches between the Human's light and dark themes, under the write lock: shows the preferred
    /// theme of the mode not currently shown (<see cref="AppearanceSettings.PreferredLightId"/> or
    /// <see cref="AppearanceSettings.PreferredDarkId"/>), then raises <see cref="AppearanceChanged"/>.
    /// </summary>
    public void ToggleMode()
    {
        lock (this.watched.Gate)
        {
            var document = this.ReadDocumentFromDisk();
            var settings = this.BuildSettings(document);
            var shown = FindTheme(settings.ThemeId) ?? ThemeCatalog.BuiltIn[0];

            // Record the theme being left as its mode's choice first, so a file that predates the pair
            // (only "theme" set) still toggles back to it.
            document[shown.Mode == ThemeMode.Dark ? "darkTheme" : "lightTheme"] = shown.Id;
            document["theme"] = shown.Mode == ThemeMode.Dark ? settings.PreferredLightId : settings.PreferredDarkId;

            this.WriteDocumentToDisk(document);
            this.watched.Current = this.BuildSettings(document);
        }

        EventRaise.Isolated(this.AppearanceChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.AppearanceChanged)));
    }

    /// <summary>The catalog entry named <paramref name="themeId"/>, or <see langword="null"/> when it is <see langword="null"/> or names none.</summary>
    /// <param name="themeId">The theme id to look up.</param>
    private static ThemeDescriptor? FindTheme(string? themeId)
    {
        foreach (var theme in ThemeCatalog.BuiltIn)
        {
            if (string.Equals(theme.Id, themeId, StringComparison.Ordinal))
            {
                return theme;
            }
        }

        return null;
    }

    /// <summary>A valid theme id: lowercase letters, digits and hyphens, 1-64 characters. The same shape a filename built from it must be safe to use as (see <c>rules.md</c>'s Name path-traversal reasoning).</summary>
    [GeneratedRegex(@"\A[a-z0-9-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeIdPattern();

    /// <summary>A valid accent colour: <c>#</c> followed by exactly six hex digits - <c>MudColorPicker</c>'s own HEX mode output.</summary>
    [GeneratedRegex(@"\A#[0-9a-fA-F]{6}\z", RegexOptions.CultureInvariant)]
    private static partial Regex AccentColorPattern();

    /// <summary>The keys of <c>appearance.json</c> that hold a Theme id.</summary>
    private static readonly string[] SelectionKeys = ["theme", "lightTheme", "darkTheme"];

    /// <summary>The Theme that replaces a retired one, or <see langword="null"/> when <paramref name="themeId"/> was never retired.</summary>
    /// <param name="themeId">A Theme id as stored.</param>
    private static string? ReplacementFor(string themeId) =>
        themeId switch
        {
            "huddle" or "custom-accent" => ThemeCatalog.DefaultLightId,
            "huddle-dark" or "custom-accent-dark" => ThemeCatalog.DefaultDarkId,
            _ => null,
        };

    /// <summary>
    /// Moves a file written for the Huddle and Custom Accent Themes onto Lumen, in place: a retired id in <c>theme</c>,
    /// <c>lightTheme</c> or <c>darkTheme</c> becomes its replacement, and the <c>accentColor</c> a Custom Accent choice
    /// carried becomes <c>customizations[replacement].palette.primary</c> (never over a colour already there). An
    /// <c>accentColor</c> with no Custom Accent choice to move into, and one that is not <c>#rrggbb</c>, stay as found - the
    /// second with a warning - and an id that was never a Theme is left for <see cref="BuildSettings"/> to warn about.
    /// </summary>
    /// <param name="document">The parsed file; edited in place.</param>
    /// <returns>Whether the document changed and so must be written back.</returns>
    private bool MigrateRetiredThemes(JsonObject document)
    {
        var changed = false;
        HashSet<string> accentTargets = new(StringComparer.Ordinal);

        foreach (var key in SelectionKeys)
        {
            if (document[key] is JsonValue value && value.TryGetValue<string>(out var id) && ReplacementFor(id) is { } replacement)
            {
                if (id.StartsWith("custom-accent", StringComparison.Ordinal))
                {
                    _ = accentTargets.Add(replacement);
                }

                document[key] = replacement;
                changed = true;
            }
        }

        if (accentTargets.Count == 0 || document["accentColor"] is not { } accentNode)
        {
            return changed;
        }

        var accent = accentNode is JsonValue accentValue && accentValue.TryGetValue<string>(out var text) ? text : null;
        if (accent is null || !AccentColorPattern().IsMatch(accent))
        {
            this.logger.LogWarning(
                "Appearance file '{Path}' sets accentColor '{AccentColor}', which is not a valid #rrggbb colour; it is not moved into a customization and is left in the file.",
                this.path,
                accent ?? accentNode.ToJsonString());
            return changed;
        }

        var customizations = EnsureObject(document, "customizations");
        foreach (var target in accentTargets)
        {
            var palette = EnsureObject(EnsureObject(customizations, target), "palette");
            if (!palette.ContainsKey("primary"))
            {
                palette["primary"] = accent;
            }
        }

        _ = document.Remove("accentColor");
        return true;
    }

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
    /// validating <c>theme</c> against <see cref="ThemeCatalog"/> and <c>accentColor</c> against
    /// <see cref="AccentColorPattern"/>, logging one warning for each if it is rejected.
    /// </summary>
    /// <param name="document">The parsed <c>appearance.json</c> object, just read from (or about to be written to) disk.</param>
    private AppearanceSettings BuildSettings(JsonObject document)
    {
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
                this.logger.LogWarning(
                    "Appearance file '{Path}' selects theme '{ThemeId}', which is not a known theme; the default theme is used instead and the file is left unchanged.",
                    this.path,
                    shown);
            }
        }

        var active = FindTheme(themeId) ?? ThemeCatalog.BuiltIn[0];
        var lightThemeId = this.ReadPreferred(document, "lightTheme", ThemeMode.Light, active);
        var darkThemeId = this.ReadPreferred(document, "darkTheme", ThemeMode.Dark, active);

        return new AppearanceSettings(themeId, lightThemeId, darkThemeId, this.ReadCustomizations(document));
    }

    /// <summary>
    /// Reads <c>customizations</c>, keeping what a skin file would accept and dropping the rest with one warning each, so a bad
    /// entry never reaches a render. The file is left exactly as it was: fixing the entry restores it with no further edit.
    /// </summary>
    /// <param name="document">The parsed <c>appearance.json</c> object.</param>
    private Dictionary<string, SkinCustomization> ReadCustomizations(JsonObject document)
    {
        Dictionary<string, SkinCustomization> result = new(StringComparer.Ordinal);

        if (!document.TryGetPropertyValue("customizations", out var node) || node is null)
        {
            return result;
        }

        if (node is not JsonObject entries)
        {
            this.WarnCustomization("customizations", "is not an object");
            return result;
        }

        foreach (var (themeId, body) in entries)
        {
            var theme = FindTheme(themeId);
            if (theme is null)
            {
                this.WarnCustomization(themeId, "names no known theme");
                continue;
            }

            if (body is not JsonObject sections)
            {
                this.WarnCustomization(themeId, "is not an object");
                continue;
            }

            foreach (var section in sections.Select(static pair => pair.Key).Where(static key => key is not ("palette" or "tokens")))
            {
                this.WarnCustomization(themeId, $"{section}: unknown section; expected palette or tokens");
            }

            var requested = new SkinCustomization(
                this.ReadStrings(themeId, "palette", sections),
                this.ReadStrings(themeId, "tokens", sections));
            var clean = SkinLoader.Sanitize(theme.Skin, requested, problem => this.WarnCustomization(themeId, problem));

            if (!clean.IsEmpty)
            {
                result[themeId] = clean;
            }
        }

        return result;
    }

    /// <summary>Reads one section of a customization as string-to-string, warning about and skipping a value that is not a string.</summary>
    /// <param name="themeId">The Theme id the entry belongs to, for the warning.</param>
    /// <param name="section">The section name: <c>palette</c> or <c>tokens</c>.</param>
    /// <param name="sections">The entry's object.</param>
    private Dictionary<string, string> ReadStrings(string themeId, string section, JsonObject sections)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);

        if (!sections.TryGetPropertyValue(section, out var node))
        {
            return values;
        }

        if (node is not JsonObject map)
        {
            this.WarnCustomization(themeId, $"{section} is not an object");
            return values;
        }

        foreach (var (key, value) in map)
        {
            if (value is JsonValue text && text.TryGetValue<string>(out var content))
            {
                values[key] = content;
            }
            else
            {
                this.WarnCustomization(themeId, $"{section}.{key}: must be a string");
            }
        }

        return values;
    }

    /// <summary>Logs the one warning a dropped customization gets.</summary>
    /// <param name="entry">What the warning is about: a Theme id, or <c>customizations</c> itself.</param>
    /// <param name="problem">What is wrong with it.</param>
    private void WarnCustomization(string entry, string problem)
    {
        this.logger.LogWarning(
            "Appearance file '{Path}' customization '{Entry}': {Problem}; it is ignored and the file is left unchanged.",
            this.path,
            entry,
            problem);
    }

    /// <summary>
    /// Reads the stored light or dark choice at <paramref name="key"/>, warning and ignoring one that
    /// names no theme or a theme of the other mode. When the key is absent and the theme being shown
    /// is of <paramref name="mode"/>, that theme is the choice - which is how a file written before the
    /// pair existed still toggles to the theme it was using.
    /// </summary>
    /// <param name="document">The parsed <c>appearance.json</c> object.</param>
    /// <param name="key">The JSON key, <c>lightTheme</c> or <c>darkTheme</c>.</param>
    /// <param name="mode">The mode the stored theme must have.</param>
    /// <param name="active">The theme being shown.</param>
    private string? ReadPreferred(JsonObject document, string key, ThemeMode mode, ThemeDescriptor active)
    {
        if (document.TryGetPropertyValue(key, out var node) && node is not null)
        {
            var candidate = node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            var theme = FindTheme(candidate);

            if (theme is not null && theme.Mode == mode)
            {
                return theme.Id;
            }

            this.logger.LogWarning(
                "Appearance file '{Path}' sets {Key} to '{ThemeId}', which is not a known {Mode} theme; the default is used instead and the file is left unchanged.",
                this.path,
                key,
                candidate ?? node.ToJsonString(),
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
                mode);
#pragma warning restore HAA0601
        }

        return active.Mode == mode ? active.Id : null;
    }

    /// <summary>
    /// Reads the raw selection file from disk into a mutable <see cref="JsonObject"/>, tolerating
    /// everything short of a locked file: a missing file yields an empty object (the normal
    /// first-run case, so this never creates the file just to read it), and malformed JSON — or a
    /// top-level value that is not a JSON object — logs a warning naming <see cref="path"/> and also
    /// yields an empty object, falling back to the defaults wholesale
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
            this.logger.LogWarning(ex, "Could not parse appearance file '{Path}'; falling back to the default appearance.", this.path);
            return new JsonObject();
        }
    }

    /// <summary>Serialises <paramref name="document"/> to <see cref="path"/> as indented, human-editable JSON.</summary>
    /// <param name="document">The document to write, unknown top-level keys and all - see the class remarks.</param>
    private void WriteDocumentToDisk(JsonObject document)
    {
        var json = document.ToJsonString(StoreJson.Indented);
        AtomicFile.WriteText(this.path, json);
    }

    /// <summary>
    /// Reads and parses <paramref name="path"/>'s current contents, throwing <see cref="JsonException"/>
    /// on malformed JSON or a non-object top level - the two callers each react to a parse failure
    /// differently, so the parsing itself carries no fallback policy of its own. Mirrors
    /// <c>PromptStore.ParseOverridesFile</c>.
    /// </summary>
    /// <param name="path">The selection file's path. Always exists; callers check <see cref="File.Exists(string)"/> first.</param>
    private static JsonObject ParseDocumentFile(string path)
    {
        var json = File.ReadAllText(path);
        var node = JsonNode.Parse(json);
        return node as JsonObject ?? throw new JsonException($"Expected a JSON object at the top level of '{path}'.");
    }

    /// <summary>
    /// Rebuilds the resolved snapshot for an external edit to <see cref="path"/>, once the watcher's
    /// debounce settles, retrying through <see cref="RetryingFileRead"/> so an in-place write caught
    /// mid-save does not flicker back to the default for the width of a save. If every attempt fails it keeps
    /// the previous snapshot by returning <see langword="null"/>.
    /// </summary>
    /// <returns>
    /// The freshly parsed document; an empty object if <see cref="path"/> no longer exists (a
    /// legitimate delete); or <see langword="null"/> if every attempt failed.
    /// </returns>
    private JsonObject? ReadDocumentForWatcherRebuild() =>
        RetryingFileRead.RereadKeepingPrevious(this.path, static () => new JsonObject(), () => ParseDocumentFile(this.path), this.logger, "appearance file", "appearance");

    /// <summary>Stops watching <see cref="path"/> and releases the debounce timer.</summary>
    public void Dispose() => this.watched.Dispose();
}
