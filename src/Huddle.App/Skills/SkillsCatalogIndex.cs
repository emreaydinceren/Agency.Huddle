using System.Collections.Frozen;
using System.Text.Json;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Skills;

/// <summary>One skill the catalog offers (Spec §CatalogEntry).</summary>
/// <param name="Id">Stable id, <c>publisher/repo/skill</c>.</param>
/// <param name="Name">The skill's folder name.</param>
/// <param name="Description">The description from the skill's frontmatter.</param>
/// <param name="Publisher">Who publishes the skill.</param>
/// <param name="Repo">The repository URL.</param>
/// <param name="Path">The skill's folder inside the repository.</param>
/// <param name="Link">A commit-pinned browse URL.</param>
/// <param name="Commit">The commit the entry was generated from.</param>
/// <param name="License">The license identifier.</param>
/// <param name="Category">The catalog category.</param>
/// <param name="SubCategory">The catalog Sub Category; skills sharing one overlap.</param>
/// <param name="Requires">What the skill needs on the machine.</param>
internal sealed record SkillsCatalogEntry(
    string Id,
    string Name,
    string Description,
    string Publisher,
    string Repo,
    string Path,
    string Link,
    string Commit,
    string License,
    string Category,
    string SubCategory,
    IReadOnlyList<string> Requires);

/// <summary>
/// The skills catalog for the picker and the import (SK-1, SK-2): the embedded <c>SkillsCatalog.json</c>,
/// replaced entirely by <c>&lt;DataDir&gt;/SkillsCatalog.json</c> when that file parses.
/// </summary>
internal sealed class SkillsCatalogIndex
{
    private const string ResourceName = "Skills/SkillsCatalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string userFile;

    private readonly ILogger<SkillsCatalogIndex> logger;

    private readonly Lazy<List<SkillsCatalogEntry>> builtIn = new(SkillsCatalogIndex.ReadBuiltIn);

    private volatile Snapshot snapshot = Snapshot.Empty;

    /// <summary>Creates the index and loads the catalog.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="logger">Receives a warning when the user copy is malformed.</param>
    public SkillsCatalogIndex(IOptions<TeamOptions> options, ILogger<SkillsCatalogIndex> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this.userFile = System.IO.Path.Combine(options.Value.DataDir, TeamPaths.SkillsCatalogFile);
        this.logger = logger;
        this.Reload();
    }

    /// <summary>Every catalog entry, in file order.</summary>
    public IReadOnlyList<SkillsCatalogEntry> All => this.snapshot.Entries;

    /// <summary>Finds an entry by its id.</summary>
    /// <param name="id">The entry id.</param>
    /// <returns>The entry, or <see langword="null"/> when none has that id.</returns>
    public SkillsCatalogEntry? FindById(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return this.snapshot.ById.GetValueOrDefault(id);
    }

    /// <summary>Finds the first entry with a name.</summary>
    /// <param name="name">The skill name.</param>
    /// <returns>The first entry with that name, or <see langword="null"/> when none has it.</returns>
    public SkillsCatalogEntry? FindByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return this.snapshot.ByName.GetValueOrDefault(name);
    }

    /// <summary>Finds an entry by repository URL and path; a <c>.git</c> suffix and trailing slashes are ignored.</summary>
    /// <param name="repo">The repository URL.</param>
    /// <param name="path">The skill's folder inside the repository.</param>
    /// <returns>The entry, or <see langword="null"/> when none matches.</returns>
    public SkillsCatalogEntry? FindByRepoPath(string repo, string path)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(path);
        return this.snapshot.ByRepoPath.GetValueOrDefault(SkillsCatalogIndex.RepoPathKey(repo, path));
    }

    /// <summary>The Sub Category of a skill, by name.</summary>
    /// <param name="skillName">The skill name.</param>
    /// <returns>The Sub Category, or <see langword="null"/> when the catalog does not know the skill.</returns>
    public string? SubCategoryOf(string skillName) => this.FindByName(skillName)?.SubCategory;

    /// <summary>Reads the catalog again: the user copy when it parses, otherwise the built-in one.</summary>
    public void Reload()
    {
        List<SkillsCatalogEntry>? entries = this.ReadUserCopy();
        this.snapshot = Snapshot.Create(entries ?? this.builtIn.Value);
    }

    /// <summary>Reads the user copy.</summary>
    /// <returns>The entries, or <see langword="null"/> when there is no copy or it is malformed.</returns>
    private List<SkillsCatalogEntry>? ReadUserCopy()
    {
        if (!File.Exists(this.userFile))
        {
            return null;
        }

        try
        {
            List<SkillsCatalogEntry>? entries = SkillsCatalogIndex.Parse(File.ReadAllText(this.userFile));
            if (entries is null)
            {
                this.logger.LogWarning("Skills catalog '{File}' is malformed; using the built-in catalog.", this.userFile);
            }

            return entries;
        }
        catch (IOException ex)
        {
            this.logger.LogWarning(ex, "Skills catalog '{File}' could not be read; using the built-in catalog.", this.userFile);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            this.logger.LogWarning(ex, "Skills catalog '{File}' could not be read; using the built-in catalog.", this.userFile);
            return null;
        }
    }

    /// <summary>Parses catalog JSON.</summary>
    /// <param name="json">The file text.</param>
    /// <returns>The entries, or <see langword="null"/> when the text is not a catalog array of complete entries.</returns>
    private static List<SkillsCatalogEntry>? Parse(string json)
    {
        List<SkillsCatalogEntry?>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<SkillsCatalogEntry?>>(json, SkillsCatalogIndex.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (raw is null)
        {
            return null;
        }

        List<SkillsCatalogEntry> entries = [];
        foreach (SkillsCatalogEntry? entry in raw)
        {
            if (entry is null
                || string.IsNullOrWhiteSpace(entry.Id)
                || string.IsNullOrWhiteSpace(entry.Name)
                || string.IsNullOrWhiteSpace(entry.Repo)
                || entry.Path is null)
            {
                return null;
            }

            entries.Add(entry with
            {
                Description = entry.Description ?? string.Empty,
                Publisher = entry.Publisher ?? string.Empty,
                Link = entry.Link ?? string.Empty,
                Commit = entry.Commit ?? string.Empty,
                License = entry.License ?? string.Empty,
                Category = entry.Category ?? string.Empty,
                SubCategory = entry.SubCategory ?? string.Empty,
                Requires = entry.Requires ?? [],
            });
        }

        return entries;
    }

    /// <summary>Reads the embedded catalog.</summary>
    /// <returns>The built-in entries.</returns>
    /// <exception cref="InvalidOperationException">The resource is missing or does not parse.</exception>
    private static List<SkillsCatalogEntry> ReadBuiltIn()
    {
        using Stream? stream = typeof(SkillsCatalogIndex).Assembly.GetManifestResourceStream(SkillsCatalogIndex.ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{SkillsCatalogIndex.ResourceName}' is missing.");
        using StreamReader reader = new(stream);
        return SkillsCatalogIndex.Parse(reader.ReadToEnd())
            ?? throw new InvalidOperationException($"Embedded resource '{SkillsCatalogIndex.ResourceName}' is malformed.");
    }

    /// <summary>The lookup key of a repository URL and path, with <c>.git</c> and slashes trimmed and case ignored.</summary>
    /// <param name="repo">The repository URL.</param>
    /// <param name="path">The folder inside the repository.</param>
    /// <returns>The key.</returns>
    private static string RepoPathKey(string repo, string path)
    {
        string trimmedRepo = repo.Trim().TrimEnd('/');
        if (trimmedRepo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            trimmedRepo = trimmedRepo[..^4].TrimEnd('/');
        }

        return string.Concat(trimmedRepo.ToUpperInvariant(), "\n", path.Trim().Trim('/'));
    }

    /// <summary>One loaded catalog and its indexes.</summary>
    private sealed class Snapshot
    {
        public static readonly Snapshot Empty = Snapshot.Create([]);

        private Snapshot(
            IReadOnlyList<SkillsCatalogEntry> entries,
            FrozenDictionary<string, SkillsCatalogEntry> byId,
            FrozenDictionary<string, SkillsCatalogEntry> byName,
            FrozenDictionary<string, SkillsCatalogEntry> byRepoPath)
        {
            this.Entries = entries;
            this.ById = byId;
            this.ByName = byName;
            this.ByRepoPath = byRepoPath;
        }

        public IReadOnlyList<SkillsCatalogEntry> Entries { get; }

        public FrozenDictionary<string, SkillsCatalogEntry> ById { get; }

        public FrozenDictionary<string, SkillsCatalogEntry> ByName { get; }

        public FrozenDictionary<string, SkillsCatalogEntry> ByRepoPath { get; }

        /// <summary>Indexes a list of entries; the first entry wins a duplicate key.</summary>
        /// <param name="entries">The entries.</param>
        /// <returns>The snapshot.</returns>
        public static Snapshot Create(IReadOnlyList<SkillsCatalogEntry> entries)
        {
            Dictionary<string, SkillsCatalogEntry> byId = new(StringComparer.Ordinal);
            Dictionary<string, SkillsCatalogEntry> byName = new(StringComparer.Ordinal);
            Dictionary<string, SkillsCatalogEntry> byRepoPath = new(StringComparer.Ordinal);
            foreach (SkillsCatalogEntry entry in entries)
            {
                _ = byId.TryAdd(entry.Id, entry);
                _ = byName.TryAdd(entry.Name, entry);
                _ = byRepoPath.TryAdd(SkillsCatalogIndex.RepoPathKey(entry.Repo, entry.Path), entry);
            }

            return new Snapshot(
                entries,
                byId.ToFrozenDictionary(StringComparer.Ordinal),
                byName.ToFrozenDictionary(StringComparer.Ordinal),
                byRepoPath.ToFrozenDictionary(StringComparer.Ordinal));
        }
    }
}
