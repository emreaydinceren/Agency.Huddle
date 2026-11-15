using Agency.Huddle.App.Acp;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>Gathers a Task's <see cref="ProofContext"/> from the live stores so <see cref="ProofEvaluator"/> can stay pure.</summary>
/// <param name="tasks">The Task store, for the Task's children.</param>
/// <param name="personas">The Persona store, for role coverage.</param>
/// <param name="gates">The gate service, for the Team's Process and role coverage.</param>
/// <param name="teams">The Team catalog, for the roster.</param>
/// <param name="resolver">Resolves an attached path line to a Library path.</param>
/// <param name="history">The document history store.</param>
internal sealed class ProofContextBuilder(
    TaskStore tasks,
    PersonaStore personas,
    ProcessGateService gates,
    ITeamCatalog teams,
    LibraryPathResolver resolver,
    DocHistoryStore history)
{
    private const string MarkdownExtension = ".md";

    /// <summary>Builds the context for <paramref name="task"/>.</summary>
    /// <param name="task">The Task.</param>
    /// <returns>The context.</returns>
    public ProofContext Build(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        string team = task.Location.Team;
        ProcessDocument? process = gates.ProcessOf(team);
        IReadOnlyList<EvidenceLine> evidence = task.Evidence;
        List<string> roster = [.. teams.Find(team)?.Members ?? []];
        List<TaskItem> children = [.. tasks.All.Where(candidate => candidate.Parent == task.Id)];
        List<ProofDocument> documents = [.. this.AttachedDocuments(task)];
        IEnumerable<string> names = evidence.Select(static line => line.Author)
            .Concat(roster)
            .Concat(documents.SelectMany(static document => document.History.Select(static entry => entry.Actor)));
        return new ProofContext(
            process?.DefinitionOfDone ?? [],
            gates.EnabledGates(team),
            TaskService.HasBugMarker(task),
            evidence,
            this.CoverageFor(names),
            roster,
            children,
            documents);
    }

    /// <summary>Maps each named Agent to the coverage of their role; names with no Persona or role are left out.</summary>
    /// <param name="names">The names to look up.</param>
    /// <returns>Name to comma-joined coverage.</returns>
    internal IReadOnlyDictionary<string, string> CoverageFor(IEnumerable<string> names)
    {
        Dictionary<string, string> covers = new(StringComparer.Ordinal);
        foreach (string name in names.Where(static name => name.Length > 0).Distinct(StringComparer.Ordinal))
        {
            if (personas.RoleOf(name) is { } role && gates.CoverageOf(role) is { } coverage)
            {
                covers[name] = coverage;
            }
        }

        return covers;
    }

    private List<ProofDocument> AttachedDocuments(TaskItem task)
    {
        List<ProofDocument> documents = [];
        foreach (string path in TaskAttachmentText.PathLines(task.Description))
        {
            if (resolver.TryResolveAbsolute(path, out LibraryPath? library)
                && library.IsFile
                && string.Equals(Path.GetExtension(library.FullPath), MarkdownExtension, StringComparison.OrdinalIgnoreCase))
            {
                documents.Add(new ProofDocument(Path.GetFileName(library.FullPath), history.Read(library)));
            }
        }

        return documents;
    }
}
