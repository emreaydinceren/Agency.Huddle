using System.Globalization;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Backs <see cref="IMergeGate"/> with the Team's process: resolves the Persona's Teams from its
/// <c>teams</c> line, collects the Evidence on each Team's Task files and asks
/// <see cref="ProcessGateService.CheckMerge"/> (PR-7, Spec §Gates).
/// </summary>
/// <param name="personas">Finds the Persona and its Teams line.</param>
/// <param name="tasks">Supplies the Team's Task files, whose Evidence sections the gate reads.</param>
/// <param name="gates">Decides the <c>checks-before-merge</c> gate.</param>
/// <param name="catalog">Maps a Teams-line label to the Team, so a stale label is ignored.</param>
internal sealed class ProcessMergeGate(PersonaStore personas, TaskStore tasks, ProcessGateService gates, ITeamCatalog catalog) : IMergeGate
{
    /// <inheritdoc />
    public string? RefusalFor(string personaName, string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);
        ArgumentNullException.ThrowIfNull(command);

        if (personas.Get(personaName) is not { } persona
            || !PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _))
        {
            // Fail closed: a merge or push from a Persona the gate cannot resolve is not waved through.
            return string.Create(CultureInfo.InvariantCulture, $"Cannot check the merge gate: the Persona '{personaName}' could not be resolved.");
        }

        foreach (string label in identity.Teams)
        {
            if (catalog.Find(label) is not { } team)
            {
                continue;
            }

            if (gates.CheckMerge(team.Name, this.EvidenceOf(team.Name)) is { } refusal)
            {
                return refusal.Message;
            }
        }

        return this.AcceptRefusalFor(personaName, identity, command);
    }

    /// <summary>Refuses a merge into main while an open gated Task of the Persona has no valid Accept (issue #396).</summary>
    /// <param name="personaName">The Persona running the command.</param>
    /// <param name="identity">The Persona's identity, whose Teams line names its Teams.</param>
    /// <param name="command">The shell line.</param>
    /// <returns>The refusal, or <see langword="null"/> when nothing holds the command.</returns>
    private string? AcceptRefusalFor(string personaName, PersonaIdentity identity, string command)
    {
        if (!MergesIntoMain(command))
        {
            return null;
        }

        HashSet<string> teams = new(StringComparer.OrdinalIgnoreCase);
        foreach (string label in identity.Teams)
        {
            if (catalog.Find(label) is { } team)
            {
                teams.Add(team.Name);
            }
        }

        foreach (TaskItem task in tasks.All)
        {
            if (!task.Status.IsTerminal()
                && task.IsGated
                && string.Equals(task.Assignee, personaName, StringComparison.OrdinalIgnoreCase)
                && teams.Contains(task.Location.Team)
                && Acceptance.Of(task).State != AcceptanceState.Valid)
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cannot merge: Task {task.Id} waits for the Human's Accept in Needs You, and this merge waits for it. Huddle moves the Task there when its Proof is complete. Do not ask for the Accept in chat.");
            }
        }

        return null;
    }

    /// <summary>
    /// Whether some segment merges something other than main, or pushes to main or to no named branch. A merge of
    /// <c>main</c> or <c>origin/main</c> is how a feature branch takes main and is not held.
    /// </summary>
    /// <param name="command">The shell line.</param>
    /// <returns><see langword="true"/> when the line would put work on main.</returns>
    private static bool MergesIntoMain(string command)
    {
        foreach (List<string> words in WorkDirPermissionHandler.SplitSegments(command))
        {
            if (words.Count < 2 || !WorkDirPermissionHandler.IsGit(WorkDirPermissionHandler.Unquote(words[0])))
            {
                continue;
            }

            int index = 1;
            while (index < words.Count && WorkDirPermissionHandler.Unquote(words[index]).StartsWith('-'))
            {
                string option = WorkDirPermissionHandler.Unquote(words[index]);
                index += WorkDirPermissionHandler.GitOptionsWithValue.Contains(option, StringComparer.Ordinal) ? 2 : 1;
            }

            if (index >= words.Count)
            {
                continue;
            }

            List<string> args = [.. words.Skip(index + 1).Select(WorkDirPermissionHandler.Unquote).Where(static word => !word.StartsWith('-'))];
            string subcommand = WorkDirPermissionHandler.Unquote(words[index]);
            if (string.Equals(subcommand, "merge", StringComparison.OrdinalIgnoreCase) && !args.TrueForAll(IsMainBranch))
            {
                return true;
            }

            if (string.Equals(subcommand, "push", StringComparison.OrdinalIgnoreCase)
                && (args.Count < 2 || args.Skip(1).Any(IsMainRef)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the name is main or master, local or on origin.</summary>
    /// <param name="name">The branch name as typed.</param>
    private static bool IsMainBranch(string name) =>
        name is "main" or "master" or "origin/main" or "origin/master";

    /// <summary>Whether a push refspec targets main or master, as <c>main</c>, <c>HEAD:main</c> or <c>refs/heads/main</c>.</summary>
    /// <param name="refspec">The push argument.</param>
    private static bool IsMainRef(string refspec)
    {
        string target = refspec[(refspec.LastIndexOf(':') + 1)..];
        return target is "main" or "master" or "refs/heads/main" or "refs/heads/master";
    }

    /// <summary>Reads the Evidence lines of every Task of the Team.</summary>
    /// <param name="team">The Team name.</param>
    /// <returns>All Evidence lines, in no particular order.</returns>
    private List<EvidenceLine> EvidenceOf(string team)
    {
        List<EvidenceLine> evidence = [];
        foreach (TaskItem task in tasks.All)
        {
            // Evidence on a finished Task is stale for a later merge; only open work counts.
            if (!task.Status.IsTerminal()
                && string.Equals(task.Location.Team, team, StringComparison.OrdinalIgnoreCase)
                && tasks.ReadText(task.Id) is { } text)
            {
                evidence.AddRange(EvidenceLine.ParseSection(text));
            }
        }

        return evidence;
    }
}
