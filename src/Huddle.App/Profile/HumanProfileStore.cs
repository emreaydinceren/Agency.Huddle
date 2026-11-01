using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Watching;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agency.Huddle.App.Profile;

/// <summary>
/// Stores the Human's profile in a Human-editable file at <c>{DataDir}/profile.json</c> and serves it
/// as the live <see cref="IHumanIdentity"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Appearance.AppearanceStore"/>'s sibling.</b> Same state model: one immutable
/// snapshot behind a <see langword="volatile"/> field, one <see cref="Lock"/> around writes, a
/// debounced watcher, and events raised after the write and the rebuild, outside the lock.
/// </para>
/// <para>
/// <b>A missing file is the first-run state.</b> <see cref="IsSaved"/> is <see langword="false"/> and
/// the Name falls back to <see cref="TeamOptions.HumanName"/>. Nothing creates the file except
/// <see cref="Save"/>, so the first-run redirect keeps working until the Human has saved.
/// </para>
/// <para>
/// <b>A rename raises <see cref="HumanRenamed"/> before <see cref="ProfileChanged"/></b>, the same
/// order <see cref="Acp.PersonaStore"/> raises <c>PersonaRenamed</c> before <c>PersonasChanged</c>, so
/// <see cref="HumanRenameCascade"/> has moved the Avatar, Tasks and directory row before anything
/// repaints. The Names are compared ordinally, so a case-only rename still cascades.
/// </para>
/// </remarks>
internal sealed class HumanProfileStore : IHumanIdentity, IDisposable
{
    private const string FileName = "profile.json";

    private readonly string path;
    private readonly string defaultName;
    private readonly ILogger<HumanProfileStore> logger;

    // The whole resolved state, swapped in as one immutable reference so readers never lock.
    private readonly WatchedFileStore<Snapshot> watched;

    /// <summary>Loads (or defaults) the profile file at <c>{DataDir}/profile.json</c>.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/> and the first-run default <see cref="TeamOptions.HumanName"/>.</param>
    /// <param name="logger">Warns when the file fails to parse or names an invalid Name.</param>
    public HumanProfileStore(IOptions<TeamOptions> options, ILogger<HumanProfileStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.defaultName = options.Value.HumanName;
        this.path = Path.Combine(options.Value.DataDir, FileName);

        var directory = Path.GetDirectoryName(this.path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException($"'{this.path}' has no parent directory to watch.");
        }

        Directory.CreateDirectory(directory);

        var initial = new Snapshot(new HumanProfile(this.defaultName, null, null, null), Saved: false);
        this.watched = new WatchedFileStore<Snapshot>(
            directory,
            FileName,
            this.BuildSnapshot(this.ReadDocumentFromDisk(), initial),
            this.ReloadForWatcher,
            (before, after) => after == before ? null : this.CaptureRaise(before, after),
            this.logger,
            nameof(HumanProfileStore));
    }

    /// <summary>Raised when the Human's Name changes, before <see cref="ProfileChanged"/>, outside the write lock.</summary>
    public event Action<HumanRenamed>? HumanRenamed;

    /// <inheritdoc />
    public event Action? ProfileChanged;

    /// <summary>The absolute path to <c>profile.json</c>, whether or not it exists yet.</summary>
    public string FilePath => this.path;

    /// <inheritdoc />
    public HumanProfile Current => this.watched.Current.Profile;

    /// <inheritdoc />
    public string Name => this.watched.Current.Profile.Name;

    /// <inheritdoc />
    public bool IsSaved => this.watched.Current.Saved;

    /// <inheritdoc />
    public bool Matches(string? nameOrAlias)
    {
        if (string.IsNullOrEmpty(nameOrAlias))
        {
            return false;
        }

        var profile = this.watched.Current.Profile;
        return string.Equals(nameOrAlias, profile.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(nameOrAlias, profile.Alias, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Writes <paramref name="profile"/> to <c>profile.json</c>, keeping any unknown keys already in
    /// the file, then raises <see cref="HumanRenamed"/> if the Name changed and
    /// <see cref="ProfileChanged"/>. The caller validates first, with <see cref="HumanProfileValidator"/>.
    /// </summary>
    /// <param name="profile">The profile to store. Blank optional fields are stored as absent.</param>
    public void Save(HumanProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Name);

        var normalised = Normalise(profile);
        Snapshot before;
        Snapshot after;
        lock (this.watched.Gate)
        {
            before = this.watched.Current;
            var document = this.ReadDocumentFromDisk() ?? new JsonObject();
            document["name"] = normalised.Name;
            SetOrRemove(document, "alias", normalised.Alias);
            SetOrRemove(document, "title", normalised.Title);
            SetOrRemove(document, "description", normalised.Description);

            AtomicFile.WriteText(this.path, document.ToJsonString(StoreJson.Indented));
            after = new Snapshot(normalised, Saved: true);
            this.watched.Current = after;
        }

        this.CaptureRaise(before, after)();
    }

    /// <summary>Stops watching the file and releases the debounce timer.</summary>
    public void Dispose()
    {
        this.watched.Dispose();
    }

    /// <summary>Trims every field and turns a blank optional field into <see langword="null"/>.</summary>
    /// <param name="profile">The profile as entered.</param>
    private static HumanProfile Normalise(HumanProfile profile) => new(
        profile.Name.Trim(),
        Blank(profile.Alias),
        Blank(profile.Title),
        Blank(profile.Description));

    /// <summary>The trimmed text, or <see langword="null"/> when it is blank.</summary>
    /// <param name="text">The text to trim.</param>
    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Sets <paramref name="key"/> to <paramref name="value"/>, or removes the key when the value is <see langword="null"/>.</summary>
    /// <param name="document">The file's top-level object.</param>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value, or <see langword="null"/> to remove the key.</param>
    private static void SetOrRemove(JsonObject document, string key, string? value)
    {
        if (value is null)
        {
            document.Remove(key);
        }
        else
        {
            document[key] = value;
        }
    }

    /// <summary>Reads a string value at <paramref name="key"/>, or <see langword="null"/> when it is absent or not a string.</summary>
    /// <param name="document">The file's top-level object.</param>
    /// <param name="key">The key to read.</param>
    private static string? ReadString(JsonObject document, string key) =>
        document.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    /// <summary>Reads and parses the file, throwing <see cref="JsonException"/> on malformed JSON or a non-object top level.</summary>
    /// <param name="path">The file to read; callers check it exists first.</param>
    private static JsonObject ParseDocumentFile(string path)
    {
        var node = JsonNode.Parse(File.ReadAllText(path));
        // A top-level array or value is as unusable as malformed JSON; the caller logs the path.
        return node as JsonObject ?? throw new JsonException();
    }

    /// <summary>
    /// Builds the snapshot for <paramref name="document"/>. A missing document means unsaved. A Name
    /// that fails <see cref="NameRules.IsValidAgentName(string?)"/> keeps <paramref name="previous"/>'s
    /// Name and logs a warning; an invalid Alias is dropped the same way.
    /// </summary>
    /// <param name="document">The parsed file, or <see langword="null"/> when there is no file.</param>
    /// <param name="previous">The snapshot to fall back to for a bad Name.</param>
    private Snapshot BuildSnapshot(JsonObject? document, Snapshot previous)
    {
        if (document is null)
        {
            return new Snapshot(new HumanProfile(this.defaultName, null, null, null), Saved: false);
        }

        var name = Blank(ReadString(document, "name"));
        if (name is null || !NameRules.IsValidAgentName(name))
        {
            this.logger.LogWarning(
                "Profile file '{Path}' has name '{Name}', which is not a valid Name; keeping '{Previous}'.",
                this.path,
                name ?? string.Empty,
                previous.Profile.Name);
            name = previous.Profile.Name;
        }

        var alias = Blank(ReadString(document, "alias"));
        if (alias is not null && (!NameRules.IsValidAgentName(alias) || string.Equals(alias, name, StringComparison.OrdinalIgnoreCase)))
        {
            this.logger.LogWarning("Profile file '{Path}' has alias '{Alias}', which is not a valid Alias; it is ignored.", this.path, alias);
            alias = null;
        }

        var profile = new HumanProfile(name, alias, Blank(ReadString(document, "title")), Blank(ReadString(document, "description")));
        return new Snapshot(profile, Saved: true);
    }

    /// <summary>
    /// Reads the file. A missing file is <see langword="null"/>, the first-run state. Malformed JSON
    /// logs a warning and reads as an empty object, so the Name falls back rather than startup failing.
    /// </summary>
    private JsonObject? ReadDocumentFromDisk()
    {
        if (!File.Exists(this.path))
        {
            return null;
        }

        try
        {
            return ParseDocumentFile(this.path);
        }
        catch (JsonException ex)
        {
            this.logger.LogWarning(ex, "Could not parse profile file '{Path}'; falling back to the configured Name.", this.path);
            return new JsonObject();
        }
    }

    /// <summary>
    /// Rebuilds the snapshot for an outside edit once the watcher settles, retrying through
    /// <see cref="RetryingFileRead"/> so a mid-save read does not flicker. Keeps the previous snapshot
    /// when every attempt fails; an unchanged result is dropped by the caller, which ignores the echo of
    /// this store's own <see cref="Save"/>.
    /// </summary>
    /// <param name="before">The snapshot currently published.</param>
    /// <returns>The rebuilt snapshot, or <see langword="null"/> to keep <paramref name="before"/>.</returns>
    private Snapshot? ReloadForWatcher(Snapshot before)
    {
        JsonObject? document = null;
        if (File.Exists(this.path))
        {
            document = RetryingFileRead.Run(() => ParseDocumentFile(this.path), this.OnReadGaveUp);
            if (document is null)
            {
                return null;
            }
        }

        return this.BuildSnapshot(document, before);
    }
    /// <summary>Logs why an outside edit could not be read and keeps the previous snapshot.</summary>
    /// <param name="ex">The last failure.</param>
    private JsonObject? OnReadGaveUp(Exception ex)
    {
        this.logger.LogWarning(ex, "Could not read profile file '{Path}' after a change; keeping the previous profile.", this.path);
        return null;
    }

    /// <summary>
    /// Captures <see cref="HumanRenamed"/> (when the Name changed) and then <see cref="ProfileChanged"/>
    /// and returns the action that raises them in that order. Call it while the lock is held when the
    /// delegates must be captured under it, and invoke the result only after releasing it.
    /// </summary>
    /// <param name="before">The snapshot before the change.</param>
    /// <param name="after">The snapshot after it.</param>
    /// <returns>The action that raises the events.</returns>
    private Action CaptureRaise(Snapshot before, Snapshot after)
    {
        Action<HumanRenamed>? renamed = this.HumanRenamed;
        Action? changed = this.ProfileChanged;
        bool nameChanged = !string.Equals(before.Profile.Name, after.Profile.Name, StringComparison.Ordinal);
        return () =>
        {
            if (nameChanged)
            {
                EventRaise.Isolated(renamed, new HumanRenamed(before.Profile.Name, after.Profile.Name), ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.HumanRenamed)));
            }

            EventRaise.Isolated(changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.ProfileChanged)));
        };
    }
    /// <summary>The resolved profile and whether it came from a saved file.</summary>
    /// <param name="Profile">The resolved profile.</param>
    /// <param name="Saved">Whether <c>profile.json</c> exists.</param>
    private sealed record Snapshot(HumanProfile Profile, bool Saved);
}
