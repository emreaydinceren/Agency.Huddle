using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Live catalog of teams built from labels, persona team memberships, and team folders. It holds one
/// immutable snapshot, rebuilt on every <see cref="PersonaStore.PersonasChanged"/> and
/// <see cref="TaskStore.IndexChanged"/> event, swapped in before <see cref="Changed"/> is raised. No lock:
/// a reader always sees a whole snapshot.
/// </summary>
internal sealed class TeamCatalog : ITeamCatalog, IDisposable
{
    private readonly PersonaStore personas;
    private readonly TaskStore tasks;
    private volatile IReadOnlyList<TeamSummary> snapshot;

    /// <summary>Builds the first snapshot and subscribes to both change signals.</summary>
    /// <param name="personas">The Persona store: Team labels and each Persona's Teams.</param>
    /// <param name="tasks">The Task store: the Team and Project folders on disk.</param>
    public TeamCatalog(PersonaStore personas, TaskStore tasks)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(tasks);

        this.personas = personas;
        this.tasks = tasks;
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

    /// <summary>Unsubscribes from both change signals. Safe to call more than once.</summary>
    public void Dispose()
    {
        this.personas.PersonasChanged -= this.OnSourceChanged;
        this.tasks.IndexChanged -= this.OnSourceChanged;
    }

    /// <summary>Re-runs <see cref="Build"/> over the stores' current state.</summary>
    private IReadOnlyList<TeamSummary> Rebuild() =>
        Build(
            this.personas.Teams,
            [.. this.personas.Entries.Select(static e => (e.Name, e.Teams))],
            this.tasks.Teams);

    /// <summary>Swaps in a fresh snapshot, then raises <see cref="Changed"/> once.</summary>
    private void OnSourceChanged()
    {
        this.snapshot = this.Rebuild();
        this.Changed?.Invoke();
    }

    /// <summary>
    /// Builds the catalog of team summaries from labels, persona team memberships, and team folders.
    /// </summary>
    /// <param name="labels">Known team labels.</param>
    /// <param name="members">Persona team memberships.</param>
    /// <param name="folders">Team folders on disk.</param>
    /// <returns>Sorted list of team summaries.</returns>
    internal static IReadOnlyList<TeamSummary> Build(
        IReadOnlyList<string> labels,
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members,
        IReadOnlyList<TeamFolder> folders)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(folders);

        // Dictionary grouped by OrdinalIgnoreCase to handle case-insensitive duplicates
        Dictionary<string, TeamData> byName = new(StringComparer.OrdinalIgnoreCase);

        // Add folders first - their spelling wins for display name
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

        // Add labels that don't already exist and track spellings
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

        // Add members to their teams and track spellings
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

        // Finalize display names - use first spelling in ordinal order for entries without folders
        foreach (TeamData data in byName.Values)
        {
            if (!data.HasFolder && data.Spellings.Count > 0)
            {
                data.DisplayName = data.Spellings.OrderBy(s => s, StringComparer.Ordinal).First();
            }
        }

        // Build the result list with filtering, deduplication and sorting
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
                data.HasFolder))
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
