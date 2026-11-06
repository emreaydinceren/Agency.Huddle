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
/// <param name="timeProvider">The clock for the window after an Accept that went straight to Done; the system clock when <see langword="null"/>.</param>
internal sealed class ProcessMergeGate(PersonaStore personas, TaskStore tasks, ProcessGateService gates, ITeamCatalog catalog, TimeProvider? timeProvider = null) : IMergeGate
{
    /// <summary>
    /// How long after an Accept that took a Task straight to Done its Team may still merge (a Task with no Action has nothing
    /// left open to hold the Accept). A merge names no Task, so within this hour any merge by the Team is allowed.
    /// </summary>
    private static readonly TimeSpan AcceptedDoneWindow = TimeSpan.FromHours(1);

    /// <summary>
    /// How long a valid Accept on an open Task carries a merge. A merge names no Task, so within this time another branch's
    /// merge by the Team is allowed too; after it the Human must Accept again.
    /// </summary>
    private static readonly TimeSpan AcceptedOpenWindow = TimeSpan.FromHours(24);

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Whether the Human's Accept took the Task straight to Done less than <see cref="AcceptedDoneWindow"/> ago.</summary>
    /// <param name="task">The Task.</param>
    /// <param name="now">The time now.</param>
    private static bool InDoneWindow(TaskItem task, DateTimeOffset now) =>
        Acceptance.AcceptedIntoDoneAt(task) is { } doneAt && now - doneAt <= AcceptedDoneWindow;

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

    /// <summary>
    /// Refuses a merge into main that waits for the Human's Accept. In a Team with <c>accept-before-merge</c> it needs an
    /// accepted Task of that Team, whoever runs it (issue #401); in any other Team it waits only while an open gated Task of
    /// the Persona has no valid Accept (issue #396).
    /// </summary>
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
        HashSet<string> acceptTeams = new(StringComparer.OrdinalIgnoreCase);
        foreach (string label in identity.Teams)
        {
            if (catalog.Find(label) is { } team)
            {
                teams.Add(team.Name);
                if (gates.EnabledGates(team.Name).Contains(GateId.AcceptBeforeMerge))
                {
                    acceptTeams.Add(team.Name);
                }
            }
        }

        return this.AcceptRequiredRefusal(personaName, acceptTeams) ?? this.AssigneeRefusal(personaName, teams, acceptTeams);
    }

    /// <summary>The refusal for a Team with <c>accept-before-merge</c> that has no accepted Task to merge, or <see langword="null"/>.</summary>
    /// <param name="personaName">The Persona running the command.</param>
    /// <param name="acceptTeams">The Persona's Teams that have the gate on.</param>
    private string? AcceptRequiredRefusal(string personaName, HashSet<string> acceptTeams)
    {
        if (acceptTeams.Count == 0)
        {
            return null;
        }

        DateTimeOffset now = this.clock.GetUtcNow();
        List<TaskItem> own = [];
        TaskItem? old = null;
        foreach (TaskItem task in tasks.All)
        {
            if (!acceptTeams.Contains(task.Location.Team))
            {
                continue;
            }

            AcceptanceInfo accepted = Acceptance.Of(task);
            if (!task.Location.Closed && accepted.State == AcceptanceState.Valid && accepted.At is { } at)
            {
                if (now - at <= AcceptedOpenWindow)
                {
                    return null;
                }

                old ??= task;
            }

            if (InDoneWindow(task, now))
            {
                return null;
            }

            if (!task.Status.IsTerminal() && string.Equals(task.Assignee, personaName, StringComparison.OrdinalIgnoreCase))
            {
                own.Add(task);
            }
        }

        if (old is not null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Cannot merge: the Human's Accept on Task {old.Id} is more than 24 hours old. Ask for a fresh one: move the Task to Needs You again with update_task so the Human can Accept it. Do not ask for the Accept in chat.");
        }

        string where = own.Count == 1
            ? string.Create(CultureInfo.InvariantCulture, $"Task {own[0].Id}'s Action")
            : "the Action of the Task this merge belongs to";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Cannot merge: a merge into main waits for the Human's Accept, and no Task of your Team has one. Put the merge in {where} with update_task, for example 'Merge feat/x into main'. Huddle moves the Task to Needs You when its Proof is complete; merge after the Human accepts it. Do not ask for the Accept in chat.");
    }

    /// <summary>The older rule, for Teams without the gate: an open gated Task of the Persona with no valid Accept holds the merge.</summary>
    /// <param name="personaName">The Persona running the command.</param>
    /// <param name="teams">All the Persona's Teams.</param>
    /// <param name="acceptTeams">The ones with <c>accept-before-merge</c>, which the newer rule already decided.</param>
    private string? AssigneeRefusal(string personaName, HashSet<string> teams, HashSet<string> acceptTeams)
    {
        foreach (TaskItem task in tasks.All)
        {
            if (!task.Status.IsTerminal()
                && task.IsGated
                && string.Equals(task.Assignee, personaName, StringComparison.OrdinalIgnoreCase)
                && teams.Contains(task.Location.Team)
                && !acceptTeams.Contains(task.Location.Team)
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
    private static bool MergesIntoMain(string command) =>
        GitCommandLine.Parse(command).Exists(static call => call.WouldPutWorkOnMain);

    /// <summary>Reads the Evidence lines of every Task of the Team.</summary>
    /// <param name="team">The Team name.</param>
    /// <returns>All Evidence lines, in no particular order.</returns>
    private List<EvidenceLine> EvidenceOf(string team)
    {
        List<EvidenceLine> evidence = [];
        DateTimeOffset now = this.clock.GetUtcNow();
        foreach (TaskItem task in tasks.All)
        {
            // Evidence on a finished Task is stale for a later merge; only open work counts, and a Task the Human's Accept
            // took straight to Done counts while the merge window it opened is still open.
            if ((!task.Status.IsTerminal() || InDoneWindow(task, now))
                && string.Equals(task.Location.Team, team, StringComparison.OrdinalIgnoreCase)
                && tasks.ReadText(task.Id) is { } text)
            {
                evidence.AddRange(EvidenceLine.ParseSection(text));
            }
        }

        return evidence;
    }
}
