using System.Diagnostics.CodeAnalysis;
using System.Text;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Live catalog of teams built from labels, persona team memberships, and team folders. It holds one
/// immutable snapshot, rebuilt on every <see cref="PersonaStore.PersonasChanged"/> and
/// <see cref="TaskStore.IndexChanged"/> event, swapped in before <see cref="Changed"/> is raised. No lock:
/// a reader always sees a whole snapshot. It also watches each Team folder's <c>_team.md</c> and rebuilds
/// (debounced) when one changes, because the Task store ignores that file. It reads the file itself rather
/// than through <c>ITeamFolders</c>, which depends on this catalog.
/// </summary>
internal sealed class TeamCatalog : ITeamCatalog, IDisposable
{
    private const int TeamFileDebounceMilliseconds = 300;

    private readonly PersonaStore personas;
    private readonly TaskStore tasks;
    private readonly FileSystemWatcher teamFileWatcher;
    private readonly ITimer teamFileTimer;
    private readonly ILogger<TeamCatalog> logger;
    private volatile bool disposed;
    private volatile IReadOnlyList<TeamSummary> snapshot;

    /// <summary>Builds the first snapshot and subscribes to both change signals.</summary>
    /// <param name="personas">The Persona store: Team labels and each Persona's Teams.</param>
    /// <param name="tasks">The Task store: the Team and Project folders on disk, and the Teams root the team files live under.</param>
    /// <param name="timeProvider">Clock for the team-file debounce; defaults to the system clock.</param>
    /// <param name="logger">Reports a <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
    public TeamCatalog(PersonaStore personas, TaskStore tasks, TimeProvider? timeProvider = null, ILogger<TeamCatalog>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(tasks);

        this.logger = logger ?? NullLogger<TeamCatalog>.Instance;
        this.personas = personas;
        this.tasks = tasks;
        this.teamFileTimer = (timeProvider ?? TimeProvider.System).CreateTimer(this.OnTeamFileTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        this.teamFileWatcher = new FileSystemWatcher(tasks.RootDirectory, TeamPaths.TeamFileName)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };
        this.teamFileWatcher.Changed += this.OnTeamFileEvent;
        this.teamFileWatcher.Created += this.OnTeamFileEvent;
        this.teamFileWatcher.Deleted += this.OnTeamFileEvent;
        this.teamFileWatcher.Renamed += this.OnTeamFileEvent;
        this.teamFileWatcher.EnableRaisingEvents = true;
        this.snapshot = this.Rebuild();
        this.personas.PersonasChanged += this.OnSourceChanged;
        this.tasks.IndexChanged += this.OnSourceChanged;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<TeamSummary> Teams => this.snapshot;

    /// <inheritdoc />
    public TeamSummary? Find(string team) =>
        this.snapshot.FirstOrDefault(t => string.Equals(t.Name, team, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public bool ProjectExists(string team, string project) =>
        this.Find(team) is { } found
        && found.Projects.Any(p => string.Equals(p, project, StringComparison.OrdinalIgnoreCase));

    /// <summary>Unsubscribes from both change signals and stops the team-file watcher. Safe to call more than once.</summary>
    public void Dispose()
    {
        this.disposed = true;
        this.teamFileWatcher.Dispose();
        this.teamFileTimer.Dispose();
        this.personas.PersonasChanged -= this.OnSourceChanged;
        this.tasks.IndexChanged -= this.OnSourceChanged;
    }

    /// <summary>Re-runs <see cref="Build"/> over the stores' current state.</summary>
    private IReadOnlyList<TeamSummary> Rebuild() =>
        Build(
            this.personas.Teams,
            [.. this.personas.Entries.Select(static e => (e.Name, e.Teams))],
            this.tasks.Teams,
            this.ReadTeamFiles(),
            this.tasks.RootDirectory);

    /// <summary>Reads the <c>_team.md</c> of every Team folder; a missing or malformed file is simply absent.</summary>
    private Dictionary<string, TeamFile> ReadTeamFiles()
    {
        Dictionary<string, TeamFile> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (TeamFolder folder in this.tasks.Teams)
        {
            if (!files.ContainsKey(folder.Name) && TryReadTeamFile(Path.Combine(this.tasks.RootDirectory, folder.Name, TeamPaths.TeamFileName), out TeamFile? file))
            {
                files[folder.Name] = file;
            }
        }

        return files;
    }

    /// <summary>Reads and parses <paramref name="path"/>; an unreadable or malformed file is a miss.</summary>
    private static bool TryReadTeamFile(string path, [NotNullWhen(true)] out TeamFile? file)
    {
        file = null;
        try
        {
            return TeamFile.TryParse(File.ReadAllText(path, Encoding.UTF8), out file);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Starts the debounce for a <c>_team.md</c> that sits directly in a Team folder; anything deeper is not a team file.</summary>
    /// <param name="sender">Unused; required by the event handler shape.</param>
    /// <param name="e">Describes what changed.</param>
    private void OnTeamFileEvent(object sender, FileSystemEventArgs e)
    {
        string relative = Path.GetRelativePath(this.tasks.RootDirectory, e.FullPath);
        if (!this.disposed && relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).Length == 2)
        {
            this.teamFileTimer.Change(TimeSpan.FromMilliseconds(TeamFileDebounceMilliseconds), Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Rebuilds and raises <see cref="Changed"/> once the debounce elapses, unless the catalog was disposed meanwhile.</summary>
    /// <param name="state">Unused; required by the timer callback shape.</param>
    private void OnTeamFileTimer(object? state)
    {
        if (!this.disposed)
        {
            this.OnSourceChanged();
        }
    }

    /// <summary>Swaps in a fresh snapshot, then raises <see cref="Changed"/> once.</summary>
    private void OnSourceChanged()
    {
        this.snapshot = this.Rebuild();
        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }

    /// <summary>
    /// Builds the catalog of team summaries from labels, persona team memberships, and team folders.
    /// </summary>
    /// <param name="labels">Known team labels.</param>
    /// <param name="members">Persona team memberships.</param>
    /// <param name="folders">Team folders on disk.</param>
    /// <param name="teamFiles">Parsed <c>_team.md</c> files by Team name; a Team without an entry has no team card.</param>
    /// <param name="teamsRoot">The Teams folder; a card naming it as its workspace has none, or <see langword="null"/> to take every card as written.</param>
    /// <returns>Sorted list of team summaries.</returns>
    internal static IReadOnlyList<TeamSummary> Build(
        IReadOnlyList<string> labels,
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members,
        IReadOnlyList<TeamFolder> folders,
        IReadOnlyDictionary<string, TeamFile>? teamFiles = null,
        string? teamsRoot = null)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(folders);

        Dictionary<string, TeamFile> cards = new(teamFiles ?? new Dictionary<string, TeamFile>(), StringComparer.OrdinalIgnoreCase);

        // Dictionary grouped by OrdinalIgnoreCase to handle case-insensitive duplicates
        Dictionary<string, TeamData> byName = new(StringComparer.OrdinalIgnoreCase);

        AddFolders(byName, folders);
        AddLabels(byName, labels);
        AddMembers(byName, members);
        FinalizeDisplayNames(byName);

        return BuildSummaries(byName, cards, teamsRoot);
    }

    /// <summary>Adds the Team folders first; their spelling wins for the display name.</summary>
    /// <param name="byName">The teams gathered so far, compared without case.</param>
    /// <param name="folders">Team folders on disk.</param>
    private static void AddFolders(Dictionary<string, TeamData> byName, IReadOnlyList<TeamFolder> folders)
    {
        foreach (TeamFolder folder in folders)
        {
            if (byName.TryGetValue(folder.Name, out TeamData? existing))
            {
                // Merge projects from multiple folders with same name (case-insensitive)
                existing.Projects.AddRange(folder.Projects);
                existing.HasFolder = true;
                // Keep first folder's spelling for display name
                if (!existing.Spellings.Contains(folder.Name, StringComparer.Ordinal))
                {
                    existing.Spellings.Add(folder.Name);
                }
            }
            else
            {
                byName[folder.Name] = new TeamData
                {
                    DisplayName = folder.Name,
                    Projects = folder.Projects.ToList(),
                    HasFolder = true,
                    Spellings = [folder.Name]
                };
            }
        }
    }

    /// <summary>Adds the labels that do not already exist and tracks their spellings.</summary>
    /// <param name="byName">The teams gathered so far, compared without case.</param>
    /// <param name="labels">Known team labels.</param>
    private static void AddLabels(Dictionary<string, TeamData> byName, IReadOnlyList<string> labels)
    {
        foreach (string label in labels)
        {
            if (byName.TryGetValue(label, out TeamData? existing))
            {
                // Label already exists from a folder - track the spelling but don't override display name
                if (!existing.Spellings.Contains(label, StringComparer.Ordinal))
                {
                    existing.Spellings.Add(label);
                }
            }
            else
            {
                byName[label] = new TeamData
                {
                    DisplayName = label,
                    Projects = [],
                    HasFolder = false,
                    Spellings = [label]
                };
            }
        }
    }

    /// <summary>Adds each Persona to its Teams and tracks the spellings.</summary>
    /// <param name="byName">The teams gathered so far, compared without case.</param>
    /// <param name="members">Persona team memberships.</param>
    private static void AddMembers(Dictionary<string, TeamData> byName, IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members)
    {
        foreach ((string personaName, IReadOnlyList<string> personaTeams) in members)
        {
            foreach (string team in personaTeams)
            {
                if (!byName.TryGetValue(team, out TeamData? teamData))
                {
                    // Team not in labels or folders - still list it (correction S3)
                    teamData = new TeamData
                    {
                        DisplayName = team,
                        Projects = [],
                        HasFolder = false,
                        Spellings = [team]
                    };
                    byName[team] = teamData;
                }
                else
                {
                    // Track this spelling of the team name
                    if (!teamData.Spellings.Contains(team, StringComparer.Ordinal))
                    {
                        teamData.Spellings.Add(team);
                    }
                }

                teamData.Members.Add(personaName);
            }
        }
    }

    /// <summary>Finalises display names: the first spelling in ordinal order for entries without a folder.</summary>
    /// <param name="byName">The teams gathered so far, compared without case.</param>
    private static void FinalizeDisplayNames(Dictionary<string, TeamData> byName)
    {
        foreach (TeamData data in byName.Values)
        {
            if (!data.HasFolder && data.Spellings.Count > 0)
            {
                data.DisplayName = data.Spellings.OrderBy(s => s, StringComparer.Ordinal).First();
            }
        }
    }

    /// <summary>Builds the result list with filtering, deduplication and sorting.</summary>
    /// <param name="byName">The teams gathered, compared without case.</param>
    /// <param name="cards">Parsed <c>_team.md</c> files by Team name.</param>
    /// <param name="teamsRoot">The Teams folder, which is never a Team's workspace, or <see langword="null"/>.</param>
    /// <returns>The team summaries, sorted by name.</returns>
    private static List<TeamSummary> BuildSummaries(Dictionary<string, TeamData> byName, Dictionary<string, TeamFile> cards, string? teamsRoot)
    {
        List<TeamSummary> result = byName.Values
            .Select(data => new TeamSummary(
                data.DisplayName,
                data.Projects
                    .Where(p => !TeamNames.IsReservedProjectName(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                data.Members
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                data.HasFolder)
            {
                TeamType = cards.GetValueOrDefault(data.DisplayName)?.TeamType,
                Manager = cards.GetValueOrDefault(data.DisplayName)?.Manager,
                Workspace = cards.GetValueOrDefault(data.DisplayName)?.Workspace is { } workspace && (teamsRoot is null || !WorkspaceProbe.IsTeamsRoot(workspace, teamsRoot)) ? workspace : null,
                Owns = cards.GetValueOrDefault(data.DisplayName)?.Owns,
                Avatar = cards.GetValueOrDefault(data.DisplayName)?.Avatar ?? Avatar.None,
            })
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return result;
    }

    /// <summary>Internal data structure for building team information.</summary>
    private sealed class TeamData
    {
        /// <summary>Display name (folder spelling if available, else first label spelling).</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Projects associated with this team.</summary>
        public List<string> Projects { get; set; } = [];

        /// <summary>Members (persona names) in this team.</summary>
        public List<string> Members { get; set; } = [];

        /// <summary>All spellings of this team name from labels and personas.</summary>
        public List<string> Spellings { get; set; } = [];

        /// <summary>Whether this team has a folder on disk.</summary>
        public bool HasFolder { get; set; }
    }
}
