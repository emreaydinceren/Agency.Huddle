using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Joins the two built-in Library Roots (Teams, Agents) with the Human-editable
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
internal sealed class LibraryRootStore
{
    /// <summary>The Skill Sources root's id.</summary>
    public const string SkillSourcesRootId = "skill-sources";

    private const string FileName = "library-roots.json";
    private const string TeamsRootId = "teams";
    private const string AgentsRootId = "agents";
    private const string RawRootId = "raw";
    private const string ArchiveRootId = "archive";
    private const string PromptsRootId = "prompts";
    private const string MemoryBackupsRootId = "memory-backups";
    private const int MaxSlugLength = 64;

    private readonly IOptions<TeamOptions> options;
    private readonly AgentPaths paths;
    private readonly ILogger<LibraryRootStore> logger;
    private readonly SkillSourcesSettings? skillSources;
    private readonly ITeamCatalog? teams;
    private readonly Lock writeGate = new();
    private volatile Snapshot current;

    /// <summary>
    /// Loads the roots; with <paramref name="skillSources"/> the Skill Sources root follows its folder setting, and with
    /// <paramref name="teams"/> every Team whose workspace can be read adds a <c>workspace-{team}</c> root (D-39).
    /// </summary>
    public LibraryRootStore(IOptions<TeamOptions> options, AgentPaths paths, ILogger<LibraryRootStore> logger, SkillSourcesSettings? skillSources = null, ITeamCatalog? teams = null)
    {
        this.options = options;
        this.paths = paths;
        this.logger = logger;
        this.skillSources = skillSources;
        this.teams = teams;
        this.current = LoadInitial(options, paths, logger, SkillSourcesDirOf(options, skillSources), teams);
        if (skillSources is not null)
        {
            skillSources.Changed += this.RefreshSkillSources;
        }

        if (teams is not null)
        {
            teams.Changed += this.RefreshWorkspaces;
        }
    }

    /// <summary>Raised once per <see cref="Save"/> or <see cref="SetHidden"/> that changes the on-disk file, outside the write lock.</summary>
    public event Action? RootsChanged;

    /// <summary>Every Library Root: the two built-ins first (Teams, then Agents), then any pinned roots, in file or configuration order.</summary>
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

        EventRaise.Isolated(this.RootsChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.RootsChanged)));
    }

    /// <summary>The trust warning every surface that pins a folder shows beside it.</summary>
    public const string TrustWarning = "Anything in this folder can be read and changed from Huddle.";

    /// <summary>
    /// Pins <paramref name="path"/> under <paramref name="name"/> after the roots already pinned, refusing a path that is
    /// not an existing folder (<see cref="Save"/> does not check this itself).
    /// </summary>
    /// <param name="name">The new root's display name.</param>
    /// <param name="path">The folder to pin.</param>
    /// <returns>An error message to show the Human, or <see langword="null"/> when the folder was pinned.</returns>
    public string? TryPin(string name, string path)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);

        if (!Directory.Exists(path))
        {
            return "That folder doesn't exist.";
        }

        List<PinnedRootEntry> pinned = [.. this.Pinned, new PinnedRootEntry(name, path)];
        this.Save(pinned);
        return null;
    }

    /// <summary>Rebuilds the snapshot with the Skill Sources folder's new directory, keeping pinned roots, the hidden set and
    /// any load error, and raises <see cref="RootsChanged"/> outside the lock.</summary>
    public void RefreshSkillSources()
    {
        _ = this.Rebuild();
        EventRaise.Isolated(this.RootsChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.RootsChanged)));
    }

    /// <summary>Re-derives the roots when the Team list changed, and raises <see cref="RootsChanged"/> only if the set of roots differs.</summary>
    private void RefreshWorkspaces()
    {
        if (this.Rebuild())
        {
            EventRaise.Isolated(this.RootsChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.RootsChanged)));
        }
    }

    /// <summary>Rebuilds the snapshot from the current pinned list, hidden set and load error.</summary>
    /// <returns>Whether the roots differ from before.</returns>
    private bool Rebuild()
    {
        lock (this.writeGate)
        {
            Snapshot snapshot = this.current;
            Snapshot rebuilt = this.BuildSnapshot(snapshot.PinnedFromFile, snapshot.Hidden, snapshot.LoadError);
            this.current = rebuilt;
            return !snapshot.Roots.SequenceEqual(rebuilt.Roots);
        }
    }

    /// <summary>Hides or shows a built-in root (<c>"teams"</c>, <c>"agents"</c>, <c>"raw"</c>, <c>"archive"</c>, <c>"prompts"</c> or <c>"skill-sources"</c>) from <see cref="VisibleRoots"/>. It stays in <see cref="Roots"/> and still resolves.</summary>
    /// <param name="builtInId">The built-in root's id.</param>
    /// <param name="hidden">Whether the root should be hidden.</param>
    /// <exception cref="ArgumentException"><paramref name="builtInId"/> does not name a built-in root.</exception>
    /// <exception cref="InvalidOperationException"><c>library-roots.json</c> is malformed.</exception>
    public void SetHidden(string builtInId, bool hidden)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(builtInId);
        if (!string.Equals(builtInId, TeamsRootId, StringComparison.Ordinal)
            && !string.Equals(builtInId, AgentsRootId, StringComparison.Ordinal)
            && !string.Equals(builtInId, RawRootId, StringComparison.Ordinal)
            && !string.Equals(builtInId, ArchiveRootId, StringComparison.Ordinal)
            && !string.Equals(builtInId, PromptsRootId, StringComparison.Ordinal)
            && !string.Equals(builtInId, MemoryBackupsRootId, StringComparison.Ordinal)
            && !string.Equals(builtInId, SkillSourcesRootId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Only a built-in Library root ('teams', 'agents', 'raw', 'archive', 'prompts', 'memory-backups' or 'skill-sources') can be hidden.", nameof(builtInId));
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

        EventRaise.Isolated(this.RootsChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.RootsChanged)));
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

        EventRaise.Isolated(this.RootsChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.RootsChanged)));
    }

    /// <summary>
    /// Slugs <paramref name="name"/> into a lowercase, hyphenated id: Unicode FormD-normalised with
    /// non-spacing marks dropped, non-ASCII-alphanumerics collapsed to a single hyphen, trimmed,
    /// capped at 64 characters, and <c>"root"</c> when nothing is left. <c>"teams"</c> and
    /// <c>"agents"</c> are always treated as taken, and a collision appends <c>-2</c>, <c>-3</c>, ...
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

        HashSet<string> takenSlugs = new(taken, StringComparer.Ordinal) { TeamsRootId, AgentsRootId, RawRootId, ArchiveRootId, PromptsRootId, MemoryBackupsRootId, SkillSourcesRootId };
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
    private static Snapshot LoadInitial(IOptions<TeamOptions> options, AgentPaths paths, ILogger<LibraryRootStore> logger, string skillSourcesDir, ITeamCatalog? teams)
    {
        string filePath = Path.Combine(options.Value.DataDir, FileName);
        var (pinnedFromFile, hidden, loadError) = ReadFile(filePath, logger);
        IReadOnlyList<(string Name, string Path)> source = ToSource(options, pinnedFromFile);
        List<LibraryRoot> roots = BuildRoots(options, paths, source, logger, skillSourcesDir, teams?.Teams ?? []);
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

        return (ReadPinned(obj), ReadHidden(obj), null);
    }

    /// <summary>Reads the <c>pinned</c> array of the roots file, skipping entries without a string name and path.</summary>
    /// <param name="obj">The parsed roots file.</param>
    /// <returns>The pinned entries, or <see langword="null"/> when the file has no <c>pinned</c> array.</returns>
    private static List<PinnedRootEntry>? ReadPinned(JsonObject obj)
    {
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

        return pinnedFromFile;
    }

    /// <summary>Reads the <c>hidden</c> array of the roots file, skipping entries that are not strings.</summary>
    /// <param name="obj">The parsed roots file.</param>
    /// <returns>The hidden root ids; empty when the file has no <c>hidden</c> array.</returns>
    private static HashSet<string> ReadHidden(JsonObject obj)
    {
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

        return hidden;
    }

    /// <summary>The pinned-root source for <see cref="BuildRoots"/>: the file's list when one was saved, else the configured list.</summary>
    private static IReadOnlyList<(string Name, string Path)> ToSource(IOptions<TeamOptions> options, IReadOnlyList<PinnedRootEntry>? pinnedFromFile) =>
        pinnedFromFile is not null
            ? [.. pinnedFromFile.Select(p => (p.Name, p.Path))]
            : [.. (options.Value.Library.Roots ?? []).Select(p => (p.Name, p.Path))];

    /// <summary>Builds the built-ins plus every pinned root that resolves, skipping (with a warning) any blank or invalid path.</summary>
    /// <summary>The Skill Sources folder: the settings' answer, else <c>Team:SkillSourcesDir</c>, else <c>{DataDir}/SkillSources</c>.</summary>
    private static string SkillSourcesDirOf(IOptions<TeamOptions> options, SkillSourcesSettings? settings)
    {
        if (settings is not null)
        {
            return settings.Dir;
        }

        string dataDir = Path.GetFullPath(options.Value.DataDir);
        return string.IsNullOrWhiteSpace(options.Value.SkillSourcesDir) ? Path.Combine(dataDir, "SkillSources") : Path.GetFullPath(options.Value.SkillSourcesDir, dataDir);
    }

    private static List<LibraryRoot> BuildRoots(IOptions<TeamOptions> options, AgentPaths paths, IReadOnlyList<(string Name, string Path)> pinnedSource, ILogger logger, string skillSourcesDir, IReadOnlyList<TeamSummary> teams)
    {
        string dataDir = options.Value.DataDir;
        string teamsRoot = TrimTrailingSeparator(Path.GetFullPath(Path.Combine(dataDir, options.Value.Teams.Dir)));
        string agentsRoot = TrimTrailingSeparator(Path.GetFullPath(paths.DefinitionsRoot));
        string rawRoot = Path.GetFullPath(Path.Combine(dataDir, options.Value.Library.Raw.Dir));
        string archiveRoot = Path.GetFullPath(Path.Combine(dataDir, options.Value.Library.Archive.Dir));
        string promptsRoot = Path.GetFullPath(Path.Combine(dataDir, options.Value.Library.Prompts.Dir));
        string memoryBackupsRoot = Path.GetFullPath(Path.Combine(dataDir, options.Value.Library.MemoryBackups.Dir));

        List<LibraryRoot> roots =
        [
            new LibraryRoot(TeamsRootId, "Teams", teamsRoot, LibraryRootKind.Teams),
            new LibraryRoot(AgentsRootId, "Agents", agentsRoot, LibraryRootKind.Agents),
            new LibraryRoot(RawRootId, "Raw", rawRoot, LibraryRootKind.Raw),
            new LibraryRoot(ArchiveRootId, "Archive", archiveRoot, LibraryRootKind.Archive, ReadOnly: true),
            new LibraryRoot(PromptsRootId, "Prompts", promptsRoot, LibraryRootKind.Prompts),
            new LibraryRoot(MemoryBackupsRootId, "Memory backups", memoryBackupsRoot, LibraryRootKind.MemoryBackups, ReadOnly: true),
            new LibraryRoot(SkillSourcesRootId, "Skill Sources", TrimTrailingSeparator(Path.GetFullPath(skillSourcesDir)), LibraryRootKind.SkillSources, ReadOnly: true),
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

        AddWorkspaceRoots(roots, teams, takenSlugs);
        return roots;
    }

    /// <summary>
    /// Adds one writable <c>workspace-{team}</c> root, named "{Team} workspace", for each Team whose workspace folder can be read.
    /// An unreadable or absent workspace adds none (D-39); the Team page shows the warning.
    /// </summary>
    private static void AddWorkspaceRoots(List<LibraryRoot> roots, IReadOnlyList<TeamSummary> teams, List<string> takenSlugs)
    {
        foreach (TeamSummary team in teams)
        {
            if (string.IsNullOrWhiteSpace(team.Workspace) || WorkspaceProbe.StateOf(team.Workspace) == WorkspaceState.Unreadable)
            {
                continue;
            }

            string slug = Slug("workspace-" + team.Name, takenSlugs);
            takenSlugs.Add(slug);
            roots.Add(new LibraryRoot(slug, team.Name + " workspace", TrimTrailingSeparator(Path.GetFullPath(team.Workspace)), LibraryRootKind.Workspace));
        }
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

        string json = document.ToJsonString(StoreJson.Indented);
        string filePath = this.FilePath;
        AtomicFile.WriteText(filePath, json);
    }

    /// <summary>Re-derives <see cref="Roots"/> from <paramref name="pinnedFromFile"/> (or configuration, when <see langword="null"/>).</summary>
    private Snapshot BuildSnapshot(IReadOnlyList<PinnedRootEntry>? pinnedFromFile, IReadOnlySet<string> hidden, LibraryRootLoadError? loadError)
    {
        var source = ToSource(this.options, pinnedFromFile);
        List<LibraryRoot> roots = BuildRoots(this.options, this.paths, source, this.logger, SkillSourcesDirOf(this.options, this.skillSources), this.teams?.Teams ?? []);
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
