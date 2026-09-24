using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Owns every file under <c>{DataDir}/{Tasks.Dir}</c>: scans it into an in-memory index by
/// <see cref="TaskId"/>, keeps the list of rejected files, and lists every Team folder together
/// with its Projects and orphan status (Spec §8.1-§8.2). Writing, moving, the watcher and startup
/// reconciliation are later work (Spec §8.3-§8.5); this part of the class is scan-and-index only.
/// </summary>
internal sealed partial class TaskStore : IDisposable
{
    private readonly PersonaStore personas;
    private readonly ILogger<TaskStore> logger;

    // Guards every rebuild of `index` - the one lock this class uses for every mutation of its
    // published snapshot, so a later Write/Move (Task 5.3) and the watcher's debounced rebuild
    // (Task 5.4) share it too, rather than PersonaStore's two-lock design (Settled corrections-B2
    // D5 item 1).
    private readonly Lock writeGate = new();

    private volatile TaskSnapshot index;
    private bool disposed;

    /// <summary>
    /// Validates that <see cref="TasksOptions.Dir"/> does not resolve equal to, inside, or as a
    /// parent of <see cref="AcpOptions.TeamsDir"/>, creates the Tasks root, then scans it.
    /// </summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>, <see cref="TasksOptions.Dir"/> and <see cref="AcpOptions.TeamsDir"/>.</param>
    /// <param name="personas">Supplies the Team labels a Team folder is checked against for §8.2's orphan flag, and its <see cref="PersonaStore.PersonasChanged"/> event.</param>
    /// <param name="clock">Reserved for the startup reconciliation and watcher work later tasks add to this class; unused so far.</param>
    /// <param name="logger">Used to warn when a directory can't be enumerated or a file can't be read during the scan.</param>
    public TaskStore(IOptions<TeamOptions> options, PersonaStore personas, TimeProvider clock, ILogger<TaskStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        this.personas = personas;
        this.logger = logger;

        string tasksRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Tasks.Dir));
        string teamsRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Acp.TeamsDir));
        ThrowIfNested(tasksRoot, teamsRoot);

        this.RootDirectory = tasksRoot;
        Directory.CreateDirectory(tasksRoot);

        this.index = this.Scan();

        this.personas.PersonasChanged += this.OnPersonasChanged;
    }

    /// <summary>Raised after any rebuild of the index - so far, only <see cref="OnPersonasChanged"/>'s orphan recomputation.</summary>
    public event Action? IndexChanged;

    /// <summary>Every Task that loaded cleanly, as an immutable snapshot.</summary>
    public IReadOnlyList<TaskItem> All => this.index.All;

    /// <summary>Every file under <see cref="RootDirectory"/> that did not become a Task, with its reason.</summary>
    public IReadOnlyList<RejectedTaskFile> RejectedFiles => this.index.Rejected;

    /// <summary>Every Team folder under <see cref="RootDirectory"/>, with its Projects and orphan status.</summary>
    public IReadOnlyList<TeamFolder> Teams => this.index.Teams;

    /// <summary>The absolute path of the Tasks scan root.</summary>
    public string RootDirectory { get; }

    /// <summary>Looks up a Task by id. Returns <see langword="null"/>, never throws, when no file carries that id.</summary>
    /// <param name="id">The Task's id.</param>
    public TaskItem? Get(TaskId id) => this.index.ById.GetValueOrDefault(id);

    /// <summary>Unsubscribes from <see cref="PersonaStore.PersonasChanged"/>.</summary>
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

        this.personas.PersonasChanged -= this.OnPersonasChanged;
    }

    /// <summary>
    /// Throws when <paramref name="tasksRoot"/> and <paramref name="teamsRoot"/> are the same
    /// directory, or either contains the other, comparing with <see cref="FolderSnapshot.PathComparer"/>
    /// on separator-terminated prefixes (Settled corrections-B2 D5 item 12).
    /// </summary>
    private static void ThrowIfNested(string tasksRoot, string teamsRoot)
    {
        string tasksPrefix = tasksRoot.EndsWith(Path.DirectorySeparatorChar) ? tasksRoot : tasksRoot + Path.DirectorySeparatorChar;
        string teamsPrefix = teamsRoot.EndsWith(Path.DirectorySeparatorChar) ? teamsRoot : teamsRoot + Path.DirectorySeparatorChar;

        bool equal = FolderSnapshot.PathComparer.Equals(tasksRoot, teamsRoot);
        bool tasksInsideTeams = HasPrefix(tasksPrefix, teamsPrefix);
        bool teamsInsideTasks = HasPrefix(teamsPrefix, tasksPrefix);

        if (equal || tasksInsideTeams || teamsInsideTasks)
        {
            throw new InvalidOperationException(
                $"Team:Tasks:Dir ('{tasksRoot}') must not equal or nest with Team:Acp:TeamsDir ('{teamsRoot}').");
        }
    }

    /// <summary>True when <paramref name="value"/> starts with <paramref name="prefix"/>, compared with <see cref="FolderSnapshot.PathComparer"/>.</summary>
    private static bool HasPrefix(string value, string prefix) =>
        value.Length >= prefix.Length && FolderSnapshot.PathComparer.Equals(value[..prefix.Length], prefix);

    /// <summary>
    /// Scans <see cref="RootDirectory"/> into a fresh <see cref="TaskSnapshot"/>: the Team folders
    /// (Spec §8.2), then every ".md" file mapped, read and parsed (Spec §8.1). The only place this
    /// class touches the filesystem to build the index.
    /// </summary>
    private TaskSnapshot Scan()
    {
        (List<TeamFolder> rawTeams, Dictionary<string, string> aliasToCanonical) = this.BuildTeams();

        List<(TaskItem Task, string Path)> parsed = [];
        List<RejectedTaskFile> rejected = [];

        string[] files;
        try
        {
            files = Directory.GetFiles(this.RootDirectory, "*.md", SearchOption.AllDirectories);
        }
        catch (IOException ex)
        {
            this.logger.LogWarning(ex, "TaskStore could not enumerate '{Root}'; treating the scan as empty.", this.RootDirectory);
            files = [];
        }

        foreach (string path in files)
        {
            if (!TaskLayout.TryMap(this.RootDirectory, path, out TaskLocation? location, out string? mapError))
            {
                if (mapError is not null)
                {
                    rejected.Add(new RejectedTaskFile(path, mapError));
                }

                continue;
            }

            if (location is null)
            {
                continue;
            }

            if (aliasToCanonical.TryGetValue(location.Team, out string? canonical) &&
                !string.Equals(canonical, location.Team, StringComparison.Ordinal))
            {
                rejected.Add(new RejectedTaskFile(
                    path,
                    $"Team folder '{location.Team}' duplicates '{canonical}' (case-insensitive); its files are rejected."));
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                rejected.Add(new RejectedTaskFile(path, $"could not be read: {ex.Message}"));
                continue;
            }

            if (!TaskFileFormat.TryParse(text, path, location, out TaskItem? task, out string parseError))
            {
                rejected.Add(new RejectedTaskFile(path, parseError));
                continue;
            }

            parsed.Add((task, path));
        }

        List<TaskItem> all = [];
        Dictionary<TaskId, TaskItem> byId = [];
        foreach (IGrouping<TaskId, (TaskItem Task, string Path)> group in parsed.GroupBy(item => item.Task.Id))
        {
            List<(TaskItem Task, string Path)> items = group.ToList();
            if (items.Count == 1)
            {
                all.Add(items[0].Task);
                byId[items[0].Task.Id] = items[0].Task;
                continue;
            }

            foreach ((TaskItem Task, string Path) item in items)
            {
                string othersJoined = string.Join(
                    ", ",
                    items.Where(other => !string.Equals(other.Path, item.Path, StringComparison.Ordinal)).Select(other => other.Path));
                rejected.Add(new RejectedTaskFile(item.Path, $"duplicate id {item.Task.Id}, also in {othersJoined}"));
            }
        }

        return new TaskSnapshot(all, byId.ToFrozenDictionary(), rejected, this.RecomputeOrphans(rawTeams));
    }

    /// <summary>
    /// Enumerates Team folders directly under <see cref="RootDirectory"/> with their Project
    /// sub-folders (<see cref="Directory.GetDirectories(string)"/>, two levels - empty folders
    /// count, Spec §8.2 / Settled corrections-B2 D5 item 14). Folders differing only by case fold
    /// into one entry, the first by Ordinal winning; the alias map lets <see cref="Scan"/> reject
    /// files found under a losing folder without reading them.
    /// </summary>
    private (List<TeamFolder> Teams, Dictionary<string, string> AliasToCanonical) BuildTeams()
    {
        string[] teamDirs;
        try
        {
            teamDirs = Directory.GetDirectories(this.RootDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(ex, "TaskStore could not list Team folders under '{Root}'.", this.RootDirectory);
            teamDirs = [];
        }

        List<IGrouping<string, string>> groups = teamDirs
            .Select(Path.GetFileName)
            .Where(name => name is { Length: > 0 } && !(name.StartsWith('_') && !string.Equals(name, TaskLayout.ClosedFolder, StringComparison.Ordinal)))
            .Select(name => name!)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<TeamFolder> teams = [];
        Dictionary<string, string> aliasToCanonical = new(StringComparer.Ordinal);

        foreach (IGrouping<string, string> group in groups)
        {
            List<string> names = group.OrderBy(name => name, StringComparer.Ordinal).ToList();
            string winner = names[0];
            foreach (string name in names)
            {
                aliasToCanonical[name] = winner;
            }

            teams.Add(new TeamFolder(winner, this.ListProjects(Path.Combine(this.RootDirectory, winner)), IsOrphan: false));
        }

        return (teams, aliasToCanonical);
    }

    /// <summary>The names of <paramref name="teamPath"/>'s Project sub-folders, excluding <c>_closed</c>.</summary>
    private List<string> ListProjects(string teamPath)
    {
        try
        {
            List<string> projects = [];
            foreach (string projectDir in Directory.GetDirectories(teamPath))
            {
                string? projectName = Path.GetFileName(projectDir);
                if (projectName is { Length: > 0 } && !string.Equals(projectName, TaskLayout.ClosedFolder, StringComparison.Ordinal))
                {
                    projects.Add(projectName);
                }
            }

            return projects;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(ex, "TaskStore could not list Project folders under '{TeamPath}'.", teamPath);
            return [];
        }
    }

    /// <summary>Recomputes each folder's <see cref="TeamFolder.IsOrphan"/> against the live <see cref="PersonaStore.Teams"/> list, without rescanning any file (Spec §8.2).</summary>
    /// <param name="teams">The Team folders to recompute orphan status for.</param>
    private List<TeamFolder> RecomputeOrphans(IReadOnlyList<TeamFolder> teams)
    {
        IReadOnlyList<string> personaTeams = this.personas.Teams;
        List<TeamFolder> updated = new(teams.Count);
        foreach (TeamFolder folder in teams)
        {
            bool isOrphan = !personaTeams.Any(team => string.Equals(team, folder.Name, StringComparison.OrdinalIgnoreCase));
            updated.Add(folder with { IsOrphan = isOrphan });
        }

        return updated;
    }

    /// <summary>Recomputes every Team folder's orphan flag and republishes the snapshot, raising <see cref="IndexChanged"/> outside the lock.</summary>
    private void OnPersonasChanged()
    {
        Action? changed;
        lock (this.writeGate)
        {
            if (this.disposed)
            {
                return;
            }

            TaskSnapshot previous = this.index;
            this.index = previous with { Teams = this.RecomputeOrphans(previous.Teams) };
            changed = this.IndexChanged;
        }

        changed?.Invoke();
    }

    /// <summary>An immutable published snapshot of the scan: every valid Task, the rejected files, and the Team folders.</summary>
    /// <param name="All">Every Task that loaded cleanly.</param>
    /// <param name="ById">The same Tasks, keyed by id for <see cref="Get"/>.</param>
    /// <param name="Rejected">Every file that did not become a Task.</param>
    /// <param name="Teams">Every Team folder, with its Projects and orphan status.</param>
    private sealed record TaskSnapshot(
        IReadOnlyList<TaskItem> All,
        FrozenDictionary<TaskId, TaskItem> ById,
        IReadOnlyList<RejectedTaskFile> Rejected,
        IReadOnlyList<TeamFolder> Teams);
}
