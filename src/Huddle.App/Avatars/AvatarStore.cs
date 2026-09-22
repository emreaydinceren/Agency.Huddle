using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MudBlazor.Utilities;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Joins the Human and every Persona's Name with a Human-editable override file at
/// <c>{DataDir}/avatars.json</c>, resolving the current <see cref="Avatar"/> for a Name every render
/// reads through <see cref="Get(string)"/>. Both the Human and a Persona live in the same file, keyed
/// by Name rather than by Persona id, because the Human has no Persona file but does have a Name.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately <see cref="Appearance.AppearanceStore"/>'s sibling.</b> Same state model: the
/// resolved snapshot is one immutable dictionary behind a single <see langword="volatile"/> field,
/// one <see cref="Lock"/> around writes, a debounced <see cref="FileSystemWatcher"/>, and an event
/// raised after both the write and the rebuild - never before, or an observer reading this store from
/// inside its own handler would see stale state. <see cref="Save"/>, <see cref="Rename"/> and
/// <see cref="Remove"/> are the only writers.
/// </para>
/// <para>
/// <b>Deliberately synchronous</b>, for the same reason <see cref="Appearance.AppearanceStore"/>
/// gives: this is read from Razor renders, which cannot await, and the file involved is one small
/// JSON document.
/// </para>
/// <para>
/// <b>Tolerance</b>, identical in kind to <see cref="Appearance.AppearanceStore"/>'s: a missing file
/// is the normal first-run case, not an error - every Name resolves to <see cref="Avatar.None"/> and
/// no file is created just to read from. A malformed file logs one warning naming the path and falls
/// back to an empty set wholesale, the same tolerance a bad <c>appearance.json</c> gets. An entry for
/// a Name this store is not currently touching is kept and never silently dropped - the same rule
/// <c>appearance.json</c> gives an unknown top-level key. A <c>label</c> longer than
/// <see cref="Avatar.MaxLabelTextElements"/> is corrected on read via <see cref="Avatar.TrimLabel(string?)"/>
/// rather than rejecting the file - the same tolerance an unknown theme id gets: accept the file,
/// correct the value, and leave the file itself exactly as it was.
/// </para>
/// <para>
/// <b>Lookups are case-insensitive, matching <c>persona_models</c>' <c>COLLATE NOCASE</c> and
/// <c>MentionParser</c>'s <c>OrdinalIgnoreCase</c></b> (<c>docs/agencyteam/rules.md</c>: a duplicate
/// Name is compared case-insensitively too), while the key is preserved exactly as it was written in
/// the file. <see cref="CaseInsensitiveNodeOptions"/> is what buys this for free: a
/// <see cref="JsonObject"/> built with it compares keys with <see cref="StringComparer.OrdinalIgnoreCase"/>
/// for every lookup, indexer set and <see cref="JsonObject.Remove(string)"/>, but an <em>existing</em>
/// key's stored text is never rewritten by a lookup that merely matched it under different casing -
/// only a brand-new key takes the casing it is written with.
/// </para>
/// </remarks>
internal sealed partial class AvatarStore : IDisposable
{
    // Same reasoning and the same value as AppearanceStore.WatcherDebounceMilliseconds: an editor's
    // save commonly fires several filesystem events in a burst, so this coalesces a burst into one
    // AvatarsChanged per pause in activity rather than thrashing every observer.
    private const int WatcherDebounceMilliseconds = 500;

    // Same reasoning as AppearanceStore.WatcherInternalBufferSize: FileSystemWatcher drops events
    // with no exception and no log when its kernel buffer overflows, raising Error instead - see
    // OnWatcherError. This file is a single small document, so an overflow here is rarer still, but
    // the fix costs nothing.
    private const int WatcherInternalBufferSize = 64 * 1024;

    // Same reasoning as AppearanceStore.WatcherReadRetryAttempts/Delay: a watcher event can fire
    // while a human's editor is still mid-write, so a rebuild retries a few times with a short pause
    // before giving up and keeping the previous snapshot, rather than flickering back to empty for
    // the width of a save.
    private const int WatcherReadRetryAttempts = 3;
    private const int WatcherReadRetryDelayMilliseconds = 20;

    // See AppearanceStore.IndentedJsonOptions's remarks for why this derives from ProtocolJson.Options
    // rather than using it directly - and it matters MORE here than it does for themes. ProtocolJson.Options
    // sets no Encoder, so it inherits JavaScriptEncoder.Default, which escapes every non-ASCII character
    // as a \uXXXX sequence. A theme id is ASCII, so that default was merely noisy; an avatar label is
    // free text a Human typed - an accented name, a symbol, a single-codepoint emoji such as U+2615 -
    // and JavaScriptEncoder.Default would turn every one of those into an unreadable \uXXXX escape in a
    // file a Human is meant to open and hand-edit. UnsafeRelaxedJsonEscaping leaves all of that alone.
    //
    // It does NOT reach a label built from a UTF-16 surrogate pair - most full-colour emoji, including
    // the very one a picker would suggest first. System.Text.Json always writes a character outside the
    // Basic Multilingual Plane as a \uXXXX\uXXXX pair, for every built-in and custom JavaScriptEncoder
    // alike (verified directly: even JavaScriptEncoder.Create(UnicodeRanges.All) still escapes one).
    // There is no Encoder setting that reaches further than the BMP, so a label that needs a surrogate
    // pair is accepted, stored and rendered correctly, but is NOT spared this one cosmetic escape on
    // disk - a System.Text.Json ceiling this store cannot raise, not a defect in it. WriteIndented makes
    // everything this Encoder does reach readable in the first place.
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Gives every JsonObject this store builds or parses OrdinalIgnoreCase lookups - see the class
    // remarks' paragraph on case-insensitivity for why this is enough on its own, with no separate
    // case-folding lookup of this store's own.
    private static readonly JsonNodeOptions CaseInsensitiveNodeOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string path;
    private readonly string imageDirectory;
    private readonly ILogger<AvatarStore> logger;
    private readonly Lock writeGate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private bool disposed;

    // The whole resolved state, swapped in as one immutable reference. Readers never take a lock -
    // see the class remarks' state-model paragraph. Dictionary rather than IReadOnlyDictionary: this
    // field is private, and every value it is ever assigned is a Dictionary built fresh by
    // BuildAvatars, so CA1859 asks for the concrete type - see CSharpPrinciples.md's collections
    // guidance. Nothing outside this class ever sees the field itself; Get is the only reader.
    private volatile Dictionary<string, Avatar> avatars;

    /// <summary>Loads (or defaults) the override file at <c>{DataDir}/avatars.json</c>.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>, already absolutised by <c>ServiceCollectionExtensions</c>'s <c>PostConfigure</c>.</param>
    /// <param name="logger">Used to warn when the override file exists but fails to parse.</param>
    public AvatarStore(IOptions<TeamOptions> options, ILogger<AvatarStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.path = Path.Combine(options.Value.DataDir, "avatars.json");
        this.imageDirectory = Path.Combine(options.Value.DataDir, "avatars");

        var directory = Path.GetDirectoryName(this.path);
        if (string.IsNullOrEmpty(directory))
        {
            // TeamOptions.DataDir is always absolutised before this constructor runs (see
            // AppearanceStore's identical guard), so this only exists so the watcher below always
            // has a real directory.
            throw new InvalidOperationException($"'{this.path}' has no parent directory to watch.");
        }

        Directory.CreateDirectory(directory);

        this.avatars = this.BuildAvatars(this.ReadDocumentFromDisk());

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        this.watcher = new FileSystemWatcher(directory, "avatars.json")
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
    /// Raised after <see cref="Save"/>, <see cref="Rename"/> or <see cref="Remove"/> has written the
    /// file and rebuilt the resolved snapshot, and after an external edit to <see cref="FilePath"/> is
    /// picked up by the filesystem watcher and its debounce settles. Raised outside the write lock,
    /// for the same reason <see cref="Appearance.AppearanceStore.AppearanceChanged"/> is: a future
    /// subscriber that takes a lock of its own cannot deadlock against a concurrent write or watcher
    /// rebuild.
    /// </summary>
    public event Action? AvatarsChanged;

    /// <summary>
    /// The absolute path to the override file, <c>avatars.json</c> under <see cref="TeamOptions.DataDir"/>,
    /// whether or not it currently exists - see the class remarks on why a missing file is the normal
    /// case. Exposed so the Settings page can tell a user exactly where the file lives, the same way
    /// <see cref="Appearance.AppearanceStore.FilePath"/> does.
    /// </summary>
    public string FilePath => this.path;

    /// <summary>
    /// The absolute path to the directory an uploaded avatar image is written under,
    /// <c>{DataDir}/avatars/</c>. Created lazily by <see cref="WriteImage(ReadOnlySpan{byte}, string)"/>,
    /// not by the constructor - an installation with no avatar images uploaded yet should have no such
    /// directory, the same "absent is normal" shape the rest of this store uses.
    /// </summary>
    public string ImageDirectory => this.imageDirectory;

    /// <summary>The current <see cref="Avatar"/> for <paramref name="name"/>. Never <see langword="null"/> - <see cref="Avatar.None"/> when there is no entry.</summary>
    /// <param name="name">A Persona's or the Human's Name, matched <see cref="StringComparison.OrdinalIgnoreCase"/>.</param>
    public Avatar Get(string name)
    {
        return this.avatars.TryGetValue(name, out var avatar) ? avatar : Avatar.None;
    }

    /// <summary>
    /// Stores (or clears) <paramref name="name"/>'s avatar, under the write lock: re-reads the file so
    /// a concurrent hand-edit is not lost, writes or removes <paramref name="name"/>'s entry while
    /// leaving every other Name's entry exactly as found, writes, rebuilds the resolved snapshot,
    /// releases the lock, deletes an orphaned predecessor image if the stored image changed, and only
    /// then raises <see cref="AvatarsChanged"/>.
    /// </summary>
    /// <param name="name">A Persona's or the Human's Name, matched <see cref="StringComparison.OrdinalIgnoreCase"/>.</param>
    /// <param name="avatar">
    /// The avatar to store. An <see cref="Avatar.IsDefault"/> avatar removes <paramref name="name"/>'s
    /// entry entirely rather than writing an empty object - this is an overrides file, and absence is
    /// the default.
    /// </param>
    public void Save(string name, Avatar avatar)
    {
        ArgumentNullException.ThrowIfNull(avatar);

        string? previousImage = null;

        lock (this.writeGate)
        {
            var document = this.ReadDocumentFromDisk();

            if (document.TryGetPropertyValue(name, out var existingNode) && existingNode is JsonObject existingEntry)
            {
                previousImage = GetString(existingEntry, "image");
            }

            if (avatar.IsDefault)
            {
                document.Remove(name);
            }
            else
            {
                document[name] = BuildEntryNode(avatar);
            }

            this.WriteDocumentToDisk(document);
            this.avatars = this.BuildAvatars(document);
        }

        // A replaced (or cleared) image must not orphan its predecessor - see WriteImage's remarks
        // on why the file name carries no meaning that would let it be found again later.
        if (previousImage is not null && !string.Equals(previousImage, avatar.Image, StringComparison.Ordinal))
        {
            this.DeleteImage(previousImage);
        }

        this.AvatarsChanged?.Invoke();
    }

    /// <summary>
    /// Moves <paramref name="oldName"/>'s entry to <paramref name="newName"/>, touching no image file
    /// on disk - see <see cref="WriteImage(ReadOnlySpan{byte}, string)"/>'s remarks for why an image's
    /// file name carries no Name of its own to move. A no-op, with no write and no
    /// <see cref="AvatarsChanged"/>, when <paramref name="oldName"/> has no entry to move.
    /// </summary>
    /// <param name="oldName">The Name currently holding the entry, matched <see cref="StringComparison.OrdinalIgnoreCase"/>.</param>
    /// <param name="newName">The Name the entry moves to.</param>
    public void Rename(string oldName, string newName)
    {
        lock (this.writeGate)
        {
            var document = this.ReadDocumentFromDisk();

            if (!document.TryGetPropertyValue(oldName, out var node) || node is not JsonObject existingEntry)
            {
                return;
            }

            document.Remove(oldName);
            document[newName] = existingEntry;

            this.WriteDocumentToDisk(document);
            this.avatars = this.BuildAvatars(document);
        }

        this.AvatarsChanged?.Invoke();
    }

    /// <summary>
    /// Removes <paramref name="name"/>'s entry and deletes its image file, if it has one. A no-op,
    /// with no write, no delete and no <see cref="AvatarsChanged"/>, when <paramref name="name"/> has
    /// no entry to remove.
    /// </summary>
    /// <param name="name">The Name to remove, matched <see cref="StringComparison.OrdinalIgnoreCase"/>.</param>
    public void Remove(string name)
    {
        string? imageFile;

        lock (this.writeGate)
        {
            var document = this.ReadDocumentFromDisk();

            if (!document.TryGetPropertyValue(name, out var node) || node is not JsonObject existingEntry)
            {
                return;
            }

            imageFile = GetString(existingEntry, "image");
            document.Remove(name);

            this.WriteDocumentToDisk(document);
            this.avatars = this.BuildAvatars(document);
        }

        this.DeleteImage(imageFile);
        this.AvatarsChanged?.Invoke();
    }

    /// <summary>
    /// Writes <paramref name="bytes"/> under a newly generated, opaque file name in
    /// <see cref="ImageDirectory"/> (created if missing) and returns that file name.
    /// </summary>
    /// <remarks>
    /// The file name is <see cref="Guid.NewGuid()"/> plus <paramref name="extension"/> - never derived
    /// from the Teammate's Name, for three reasons, each a real defect this avoids:
    /// <list type="number">
    /// <item><description>A Name may contain interior spaces (<c>rules.md</c>), so a Name-derived URL would need percent-encoding at every call site.</description></item>
    /// <item><description><c>docs/agencyteam/known-limits.md</c> records that Windows reserved device names (<c>CON</c>, <c>NUL</c>, <c>COM1</c>) pass <c>NameRules</c>, and <c>CON.png</c> is a file Windows will not create.</description></item>
    /// <item><description>An opaque id means <see cref="Rename"/> touches no image file at all, and gives cache-busting for free: a replaced image gets a new id and therefore a new URL, so no query string or timestamp is needed.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="bytes">The image bytes to write.</param>
    /// <param name="extension">The file extension to write under, including the leading dot - see <see cref="AvatarImage.SniffExtension(ReadOnlySpan{byte})"/>.</param>
    /// <returns>The generated file name, relative to <see cref="ImageDirectory"/>.</returns>
    internal string WriteImage(ReadOnlySpan<byte> bytes, string extension)
    {
        Directory.CreateDirectory(this.imageDirectory);

        var fileName = Guid.NewGuid().ToString("n") + extension;
        File.WriteAllBytes(Path.Combine(this.imageDirectory, fileName), bytes);

        return fileName;
    }

    /// <summary>Deletes one image file under <see cref="ImageDirectory"/>. A no-op, never a throw, when <paramref name="fileName"/> is <see langword="null"/>, empty, or names no file on disk.</summary>
    /// <param name="fileName">The file name to delete, as previously returned by <see cref="WriteImage(ReadOnlySpan{byte}, string)"/>.</param>
    internal void DeleteImage(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return;
        }

        var filePath = Path.Combine(this.imageDirectory, fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Builds the resolved Name-to-<see cref="Avatar"/> map for <paramref name="document"/>, trimming
    /// every <c>label</c> to <see cref="Avatar.MaxLabelTextElements"/> via <see cref="Avatar.TrimLabel(string?)"/>
    /// - see the class remarks' tolerance paragraph for why this corrects on read rather than
    /// rejecting the file. An entry whose value is not a JSON object is skipped rather than thrown on,
    /// the same "keep the file, ignore what cannot be resolved" tolerance the rest of this store gives.
    /// </summary>
    /// <param name="document">The parsed <c>avatars.json</c> object, just read from (or about to be written to) disk.</param>
    private Dictionary<string, Avatar> BuildAvatars(JsonObject document)
    {
        Dictionary<string, Avatar> result = new(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, node) in document)
        {
            if (node is not JsonObject entry)
            {
                continue;
            }

            var label = Avatar.TrimLabel(GetString(entry, "label"));
            var image = GetString(entry, "image");
            var background = this.ResolveBackground(GetString(entry, "background"), name);

            result[name] = new Avatar(label, image, background);
        }

        return result;
    }

    /// <summary>
    /// Returns <paramref name="raw"/> when <see cref="MudColor"/> can parse it, and
    /// <see langword="null"/> with one logged warning when it cannot.
    /// </summary>
    /// <remarks>
    /// This is not defensive padding - it is the boundary this store is responsible for. A background
    /// reaches <c>TeammateAvatar</c> as a <see cref="MudColor"/>, and <see cref="MudColor"/>'s string
    /// constructor <b>throws</b> on anything it cannot parse (<see cref="ArgumentException"/> for
    /// <c>"not-a-colour"</c>, <see cref="FormatException"/> for <c>"zzz"</c>) rather than returning a
    /// fallback. <c>avatars.json</c> is hand-editable by design, so a Human typing <c>purple-ish</c>
    /// into it would otherwise throw during a Razor render - and a render that throws on Blazor Server
    /// tears down the circuit, so one mistyped colour would take out the page rather than one avatar.
    /// Correcting it here instead matches what this store already does to an over-long <c>label</c>,
    /// and what <c>AppearanceStore</c> does to a <c>theme</c> naming no catalogue entry: keep the file
    /// exactly as it is, warn once, and fall back - so fixing the value restores the choice with no
    /// further edit. <c>rules.md</c>: "A Model the agent does not advertise is a warning, never a
    /// failure."
    /// </remarks>
    /// <param name="raw">The raw <c>background</c> value read from the file, or <see langword="null"/> when the entry has none.</param>
    /// <param name="name">The Name whose entry carried <paramref name="raw"/>, named in the warning so the offending line can be found.</param>
    /// <returns><paramref name="raw"/> when it parses as a colour; otherwise <see langword="null"/>.</returns>
    private string? ResolveBackground(string? raw, string name)
    {
        if (raw is null)
        {
            return null;
        }

        try
        {
            _ = new MudColor(raw);
            return raw;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            this.logger.LogWarning(
                "Avatar file '{Path}' gives '{Name}' the background '{Background}', which is not a colour; that avatar falls back to the theme's own colour and the file is left unchanged.",
                this.path,
                name,
                raw);
            return null;
        }
    }

    /// <summary>Builds the JSON object for <paramref name="avatar"/>, omitting a field entirely rather than writing it as <see langword="null"/> - this is an overrides file, and absence is the default.</summary>
    /// <param name="avatar">The avatar to serialise. Never <see cref="Avatar.IsDefault"/> - the caller (<see cref="Save"/>) removes the entry instead of calling this for one.</param>
    private static JsonObject BuildEntryNode(Avatar avatar)
    {
        JsonObject entry = new(CaseInsensitiveNodeOptions);

        if (avatar.Label is not null)
        {
            entry["label"] = avatar.Label;
        }

        if (avatar.Image is not null)
        {
            entry["image"] = avatar.Image;
        }

        if (avatar.Background is not null)
        {
            entry["background"] = avatar.Background;
        }

        return entry;
    }

    /// <summary>Reads one string-valued property from <paramref name="entry"/>, or <see langword="null"/> when the property is absent, JSON <see langword="null"/>, or not a string.</summary>
    /// <param name="entry">One Name's parsed entry.</param>
    /// <param name="key">The property to read - <c>"label"</c>, <c>"image"</c> or <c>"background"</c>.</param>
    private static string? GetString(JsonObject entry, string key)
    {
        return entry.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
    }

    /// <summary>
    /// Reads the raw override file from disk into a mutable, case-insensitive <see cref="JsonObject"/>,
    /// tolerating everything short of a locked file: a missing file yields an empty object (the normal
    /// first-run case, so this never creates the file just to read it), and malformed JSON - or a
    /// top-level value that is not a JSON object - logs a warning naming <see cref="path"/> and also
    /// yields an empty object, falling back to an empty set wholesale rather than throwing and
    /// preventing startup.
    /// </summary>
    private JsonObject ReadDocumentFromDisk()
    {
        if (!File.Exists(this.path))
        {
            return new JsonObject(CaseInsensitiveNodeOptions);
        }

        try
        {
            return ParseDocumentFile(this.path);
        }
        catch (JsonException ex)
        {
            this.logger.LogWarning(ex, "Could not parse avatar overrides file '{Path}'; falling back to no avatar overrides.", this.path);
            return new JsonObject(CaseInsensitiveNodeOptions);
        }
    }

    /// <summary>Serialises <paramref name="document"/> to <see cref="path"/> as indented, human-editable JSON.</summary>
    /// <param name="document">The document to write, every other Name's entry included - see the class remarks.</param>
    private void WriteDocumentToDisk(JsonObject document)
    {
        var json = document.ToJsonString(IndentedJsonOptions);
        File.WriteAllText(this.path, json);
    }

    /// <summary>
    /// Reads and parses <paramref name="path"/>'s current contents, throwing <see cref="JsonException"/>
    /// on malformed JSON or a non-object top level - the two callers each react to a parse failure
    /// differently, so the parsing itself carries no fallback policy of its own. Mirrors
    /// <c>AppearanceStore.ParseDocumentFile</c>.
    /// </summary>
    /// <param name="path">The override file's path. Always exists; callers check <see cref="File.Exists(string)"/> first.</param>
    private static JsonObject ParseDocumentFile(string path)
    {
        var json = File.ReadAllText(path);
        var node = JsonNode.Parse(json, CaseInsensitiveNodeOptions);
        return node as JsonObject ?? throw new JsonException($"Expected a JSON object at the top level of '{path}'.");
    }

    /// <summary>
    /// Rebuilds the resolved snapshot for an external edit to <see cref="path"/>, once the watcher's
    /// debounce settles. Retries a few times first (see <see cref="WatcherReadRetryAttempts"/>) so an
    /// in-place write caught mid-save does not flicker back to empty for the width of a save; if every
    /// attempt still fails to parse, returns <see langword="null"/> so the caller
    /// (<see cref="OnDebounceElapsed"/>) keeps the previous resolved snapshot untouched. Mirrors
    /// <c>AppearanceStore.ReadDocumentForWatcherRebuild</c>'s reasoning exactly.
    /// </summary>
    /// <returns>
    /// The freshly parsed document; an empty object if <see cref="path"/> no longer exists (a
    /// legitimate delete); or <see langword="null"/> if every parse attempt failed.
    /// </returns>
    private JsonObject? ReadDocumentForWatcherRebuild()
    {
        if (!File.Exists(this.path))
        {
            return new JsonObject(CaseInsensitiveNodeOptions);
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
                    "Could not parse avatar overrides file '{Path}' after a filesystem change, even after retrying; keeping the previously resolved avatars rather than reverting to empty over what may be a mid-write race.",
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

    // See AppearanceStore.OnWatcherError's remarks: there is no way to know which change was
    // dropped, so the only correct response is the same one a normal change takes.
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        this.logger.LogWarning(
            e.GetException(),
            "AvatarStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.");

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
            // AppearanceStore.OnDebounceElapsed's remarks for why this ordering matters.
            this.avatars = this.BuildAvatars(document);
            changed = this.AvatarsChanged;
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
