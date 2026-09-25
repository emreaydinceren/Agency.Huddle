using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Library;

/// <summary>A parse failure in <c>library-roots.json</c>, with a 1-based line and column when known.</summary>
/// <param name="Message">A human-readable description of the failure.</param>
/// <param name="Line">The 1-based line the failure was found on, or <see langword="null"/> when unknown.</param>
/// <param name="Column">The 1-based column the failure was found on, or <see langword="null"/> when unknown.</param>
public sealed record LibraryRootLoadError(string Message, long? Line, long? Column);

/// <summary>
/// Joins the two built-in Library Roots (Teams, Teammates) with the Human-editable
/// <c>{DataDir}/library-roots.json</c> (pinned roots and hidden built-ins), per Spec §6.1 and
/// corrections-B3 4.1.i.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately <see cref="Agency.Huddle.App.Tasks.Views.ViewStore"/>'s sibling for malformed-file
/// behaviour: a broken file does not fall back to empty. It sets <see cref="LoadError"/>, keeps the
/// last good (or configured) snapshot, and refuses every <see cref="Save"/> or
/// <see cref="SetHidden"/>, so a write made while the file is broken can never overwrite the
/// Human's roots with a smaller set than they actually have.
/// </para>
/// <para>
/// Writing goes through a temporary file plus an atomic <see cref="File.Move(string, string, bool)"/>
/// (<see cref="Agency.Huddle.App.FileChanges.FileStateStore"/>'s pattern), so a crash mid-save
/// cannot truncate <c>library-roots.json</c>. There is deliberately no <see cref="FileSystemWatcher"/>
/// here (corrections-B3 4.1.t item 9): the file is only ever written from Settings, through this
/// store, so no external-edit reload path exists yet.
/// </para>
/// </remarks>
internal sealed class LibraryRootStore(IOptions<TeamOptions> options, TeammatePaths paths, ILogger<LibraryRootStore> logger)
{
    private const string FileName = "library-roots.json";
    private const string TeamsRootId = "teams";
    private const string TeammatesRootId = "teammates";
    private const int MaxSlugLength = 64;

    // Same reasoning as PromptStore.IndentedJsonOptions: this file only ever round-trips through
    // JsonSerializer/JsonNode, never through HTML or script, and a Human hand-edits it, so relaxed
    // escaping keeps em-dashes and angle brackets in pinned root names readable on disk.
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IOptions<TeamOptions> options = options;
    private readonly TeammatePaths paths = paths;
    private readonly ILogger<LibraryRootStore> logger = logger;
    private readonly Lock writeGate = new();
    private volatile Snapshot current = LoadInitial(options, paths, logger);

    /// <summary>Raised once per <see cref="Save"/> or <see cref="SetHidden"/> that changes the on-disk file, outside the write lock.</summary>
    public event Action? RootsChanged;

    /// <summary>Every Library Root: the two built-ins first (Teams, then Teammates), then any pinned roots, in file or configuration order.</summary>
    public IReadOnlyList<LibraryRoot> Roots => this.current.Roots;

    /// <summary><see cref="Roots"/> minus any built-in hidden via <see cref="SetHidden"/>.</summary>
    public IReadOnlyList<LibraryRoot> VisibleRoots => this.current.VisibleRoots;

    /// <summary>The pinned roots as currently saved (or, absent a save, as configured), for round-tripping through Settings.</summary>
    public IReadOnlyList<PinnedRootEntry> Pinned => this.current.Pinned;

    /// <summary>The absolute path to <c>library-roots.json</c> under <see cref="TeamOptions.DataDir"/>.</summary>
    public string FilePath => Path.Combine(this.options.Value.DataDir, FileName);

    /// <summary>Set when <c>library-roots.json</c> exists but could not be parsed; every write is refused while this is non-null.</summary>
    public LibraryRootLoadError? LoadError => this.current.LoadError;

    /// <summary>Replaces the saved pinned roots and re-derives <see cref="Roots"/>. Throws while <see cref="LoadError"/> is set.</summary>
    /// <param name="pinned">The full replacement list of pinned roots.</param>
    /// <exception cref="InvalidOperationException"><c>library-roots.json</c> is malformed.</exception>
    public void Save(IReadOnlyList<PinnedRootEntry> pinned)
    {
        ArgumentNullException.ThrowIfNull(pinned);

        lock (this.writeGate)
        {
            var snapshot = this.current;
            if (snapshot.LoadError is not null)
            {
                throw new InvalidOperationException($"'{this.FilePath}' could not be parsed; fix it by hand before saving Library roots.");
            }

            this.WriteToDisk(pinned, snapshot.Hidden);
            this.current = this.BuildSnapshot(pinned, snapshot.Hidden, loadError: null);
        }

        this.RootsChanged?.Invoke();
    }

    /// <summary>Hides or shows a built-in root (<c>"teams"</c> or <c>"teammates"</c>) from <see cref="VisibleRoots"/>. It stays in <see cref="Roots"/> and still resolves.</summary>
    /// <param name="builtInId">The built-in root's id.</param>
    /// <param name="hidden">Whether the root should be hidden.</param>
    /// <exception cref="ArgumentException"><paramref name="builtInId"/> does not name a built-in root.</exception>
    /// <exception cref="InvalidOperationException"><c>library-roots.json</c> is malformed.</exception>
    public void SetHidden(string builtInId, bool hidden)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(builtInId);
        if (!string.Equals(builtInId, TeamsRootId, StringComparison.Ordinal) && !string.Equals(builtInId, TeammatesRootId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Only a built-in Library root ('teams' or 'teammates') can be hidden.", nameof(builtInId));
        }

        lock (this.writeGate)
        {
            var snapshot = this.current;
            if (snapshot.LoadError is not null)
            {
                throw new InvalidOperationException($"'{this.FilePath}' could not be parsed; fix it by hand before changing Library roots.");
            }

            HashSet<string> newHidden = new(snapshot.Hidden, StringComparer.Ordinal);
            if (hidden)
            {
                _ = newHidden.Add(builtInId);
            }
            else
            {
                _ = newHidden.Remove(builtInId);
            }

            this.WriteToDisk(snapshot.PinnedFromFile, newHidden);
            this.current = this.BuildSnapshot(snapshot.PinnedFromFile, newHidden, loadError: null);
        }

        this.RootsChanged?.Invoke();
    }

    /// <summary>Deletes <c>library-roots.json</c> (if any) and reverts to the configured pinned roots, with nothing hidden.</summary>
    public void Reset()
    {
        lock (this.writeGate)
        {
            string filePath = this.FilePath;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            this.current = this.BuildSnapshot(pinnedFromFile: null, hidden: new HashSet<string>(StringComparer.Ordinal), loadError: null);
        }

        this.RootsChanged?.Invoke();
    }

    /// <summary>
    /// Slugs <paramref name="name"/> into a lowercase, hyphenated id: Unicode FormD-normalised with
    /// non-spacing marks dropped, non-ASCII-alphanumerics collapsed to a single hyphen, trimmed,
    /// capped at 64 characters, and <c>"root"</c> when nothing is left. <c>"teams"</c> and
    /// <c>"teammates"</c> are always treated as taken, and a collision appends <c>-2</c>, <c>-3</c>, ...
    /// </summary>
    /// <param name="name">The pinned root's display name.</param>
    /// <param name="taken">Slugs already assigned to earlier roots in this build.</param>
    internal static string Slug(string name, IReadOnlyCollection<string> taken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(taken);

        string normalized = name.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            char lower = char.ToLowerInvariant(ch);
            builder.Append(lower is (>= 'a' and <= 'z') or (>= '0' and <= '9') ? lower : '-');
        }

        string collapsed = CollapseDashes(builder.ToString());
        if (collapsed.Length > MaxSlugLength)
        {
            collapsed = CollapseDashes(collapsed[..MaxSlugLength]);
        }

        string baseSlug = collapsed.Length == 0 ? "root" : collapsed;

        HashSet<string> takenSlugs = new(taken, StringComparer.Ordinal) { TeamsRootId, TeammatesRootId };
        if (!takenSlugs.Contains(baseSlug))
        {
            return baseSlug;
        }

        int suffix = 2;
        string candidate;
        do
        {
            candidate = string.Create(CultureInfo.InvariantCulture, $"{baseSlug}-{suffix}");
            suffix++;
        }
        while (takenSlugs.Contains(candidate));

        return candidate;
    }

    /// <summary>Collapses runs of <c>-</c> to one and trims leading/trailing <c>-</c>.</summary>
    private static string CollapseDashes(string value)
    {
        StringBuilder result = new(value.Length);
        bool lastWasDash = false;
        foreach (var ch in value)
        {
            if (ch == '-')
            {
                lastWasDash = true;
                continue;
            }

            if (lastWasDash && result.Length > 0)
            {
                _ = result.Append('-');
            }

            lastWasDash = false;
            _ = result.Append(ch);
        }

        return result.ToString();
    }

    /// <summary>Builds the initial <see cref="Snapshot"/> for the primary constructor, never throwing on bad configuration or a malformed file.</summary>
    private static Snapshot LoadInitial(IOptions<TeamOptions> options, TeammatePaths paths, ILogger<LibraryRootStore> logger)
    {
        string filePath = Path.Combine(options.Value.DataDir, FileName);
        var (pinnedFromFile, hidden, loadError) = ReadFile(filePath, logger);
        IReadOnlyList<(string Name, string Path)> source = ToSource(options, pinnedFromFile);
        var roots = BuildRoots(options, paths, source, logger);
        return new Snapshot(roots, hidden, pinnedFromFile, loadError);
    }

    /// <summary>Parses <paramref name="filePath"/>, if it exists. A missing file is the normal first-run case, not an error.</summary>
    private static (IReadOnlyList<PinnedRootEntry>? PinnedFromFile, HashSet<string> Hidden, LibraryRootLoadError? LoadError) ReadFile(string filePath, ILogger logger)
    {
        if (!File.Exists(filePath))
        {
            return (null, [], null);
        }

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "'{Path}' could not be read; using the configured Library roots.", filePath);
            return (null, [], new LibraryRootLoadError(ex.Message, null, null));
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return (null, [], new LibraryRootLoadError(ex.Message, ex.LineNumber + 1, ex.BytePositionInLine + 1));
        }

        if (root is not JsonObject obj)
        {
            // Not an object (e.g. a bare array or scalar): no "pinned"/"hidden" keys to read, so this
            // falls through exactly like an object with neither key present - configured roots, none hidden.
            return (null, [], null);
        }

        List<PinnedRootEntry>? pinnedFromFile = null;
        if (obj.TryGetPropertyValue("pinned", out var pinnedNode) && pinnedNode is JsonArray pinnedArray)
        {
            pinnedFromFile = [];
            foreach (var item in pinnedArray)
            {
                if (item is JsonObject entry
                    && entry.TryGetPropertyValue("name", out var nameNode) && nameNode is JsonValue nameValue && nameValue.TryGetValue(out string? name)
                    && entry.TryGetPropertyValue("path", out var pathNode) && pathNode is JsonValue pathValue && pathValue.TryGetValue(out string? path))
                {
                    pinnedFromFile.Add(new PinnedRootEntry(name, path));
                }
            }
        }

        HashSet<string> hidden = new(StringComparer.Ordinal);
        if (obj.TryGetPropertyValue("hidden", out var hiddenNode) && hiddenNode is JsonArray hiddenArray)
        {
            foreach (var item in hiddenArray)
            {
                if (item is JsonValue value && value.TryGetValue(out string? id) && id is not null)
                {
                    _ = hidden.Add(id);
                }
            }
        }

        return (pinnedFromFile, hidden, null);
    }

    /// <summary>The pinned-root source for <see cref="BuildRoots"/>: the file's list when one was saved, else the configured list.</summary>
    private static IReadOnlyList<(string Name, string Path)> ToSource(IOptions<TeamOptions> options, IReadOnlyList<PinnedRootEntry>? pinnedFromFile) =>
        pinnedFromFile is not null
            ? [.. pinnedFromFile.Select(p => (p.Name, p.Path))]
            : [.. (options.Value.Library.Roots ?? []).Select(p => (p.Name, p.Path))];

    /// <summary>Builds the built-ins plus every pinned root that resolves, skipping (with a warning) any blank or invalid path.</summary>
    private static List<LibraryRoot> BuildRoots(IOptions<TeamOptions> options, TeammatePaths paths, IReadOnlyList<(string Name, string Path)> pinnedSource, ILogger logger)
    {
        string dataDir = options.Value.DataDir;
        string teamsRoot = TrimTrailingSeparator(Path.GetFullPath(Path.Combine(dataDir, options.Value.Teams.Dir)));
        string teammatesRoot = TrimTrailingSeparator(Path.GetFullPath(paths.DefinitionsRoot));

        List<LibraryRoot> roots =
        [
            new LibraryRoot(TeamsRootId, "Teams", teamsRoot, LibraryRootKind.Teams),
            new LibraryRoot(TeammatesRootId, "Teammates", teammatesRoot, LibraryRootKind.Teammates),
        ];

        List<string> takenSlugs = [];
        foreach (var (name, rawPath) in pinnedSource)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                logger.LogWarning("Pinned Library root '{Name}' has a blank path and was skipped.", name);
                continue;
            }

            string resolved;
            try
            {
                resolved = TrimTrailingSeparator(Path.GetFullPath(rawPath, dataDir));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                logger.LogWarning(ex, "Pinned Library root '{Name}' has an invalid path '{Path}' and was skipped.", name, rawPath);
                continue;
            }

            string slug = Slug(name, takenSlugs);
            takenSlugs.Add(slug);
            roots.Add(new LibraryRoot(slug, name, resolved, LibraryRootKind.Pinned));
        }

        return roots;
    }

    /// <summary>Trims a single trailing directory separator, except from a drive root such as <c>E:\</c>.</summary>
    private static string TrimTrailingSeparator(string fullPath)
    {
        if (fullPath.Length <= 3 || (fullPath[^1] != Path.DirectorySeparatorChar && fullPath[^1] != Path.AltDirectorySeparatorChar))
        {
            return fullPath;
        }

        return fullPath[..^1];
    }

    /// <summary>Writes <paramref name="pinned"/> (when known) and <paramref name="hidden"/> (when non-empty) to <see cref="FilePath"/> through a temp file plus an atomic move.</summary>
    private void WriteToDisk(IReadOnlyList<PinnedRootEntry>? pinned, IReadOnlySet<string> hidden)
    {
        Directory.CreateDirectory(this.options.Value.DataDir);

        JsonObject document = [];
        if (pinned is not null)
        {
            JsonArray pinnedArray = [];
            foreach (var entry in pinned)
            {
                pinnedArray.Add(new JsonObject { ["name"] = entry.Name, ["path"] = entry.Path });
            }

            document["pinned"] = pinnedArray;
        }

        if (hidden.Count > 0)
        {
            JsonArray hiddenArray = [];
            foreach (var id in hidden)
            {
                hiddenArray.Add(id);
            }

            document["hidden"] = hiddenArray;
        }

        string json = document.ToJsonString(IndentedJsonOptions);
        string filePath = this.FilePath;
        string tmpPath = filePath + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, filePath, overwrite: true);
    }

    /// <summary>Re-derives <see cref="Roots"/> from <paramref name="pinnedFromFile"/> (or configuration, when <see langword="null"/>).</summary>
    private Snapshot BuildSnapshot(IReadOnlyList<PinnedRootEntry>? pinnedFromFile, IReadOnlySet<string> hidden, LibraryRootLoadError? loadError)
    {
        var source = ToSource(this.options, pinnedFromFile);
        var roots = BuildRoots(this.options, this.paths, source, this.logger);
        return new Snapshot(roots, hidden, pinnedFromFile, loadError);
    }

    /// <summary>The immutable, single-writer/many-readers state published through the <see langword="volatile"/> <see cref="current"/> field.</summary>
    /// <param name="Roots">Every Library Root, built-ins first.</param>
    /// <param name="Hidden">The ids of hidden built-in roots.</param>
    /// <param name="PinnedFromFile">The pinned list as last saved, or <see langword="null"/> when no save has happened yet (still configuration-driven).</param>
    /// <param name="LoadError">Set when the on-disk file exists but could not be parsed.</param>
    private sealed record Snapshot(IReadOnlyList<LibraryRoot> Roots, IReadOnlySet<string> Hidden, IReadOnlyList<PinnedRootEntry>? PinnedFromFile, LibraryRootLoadError? LoadError)
    {
        public IReadOnlyList<LibraryRoot> VisibleRoots => [.. this.Roots.Where(r => !this.Hidden.Contains(r.Id))];

        public IReadOnlyList<PinnedRootEntry> Pinned => this.PinnedFromFile ?? [.. this.Roots.Where(r => r.Kind == LibraryRootKind.Pinned).Select(r => new PinnedRootEntry(r.DisplayName, r.FullPath))];
    }
}
