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
/// <param name="levels">Resolves the Needs You level of a Task, so a local merge of a released Task needs no Accept (ADR-0087); <see langword="null"/> means Ask me everywhere.</param>
internal sealed class ProcessMergeGate(PersonaStore personas, TaskStore tasks, ProcessGateService gates, ITeamCatalog catalog, TimeProvider? timeProvider = null, NeedsYouLevels? levels = null) : IMergeGate
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

        return this.AcceptRequiredRefusal(personaName, acceptTeams, command) ?? this.AssigneeRefusal(personaName, teams, acceptTeams);
    }

    /// <summary>The refusal for a Team with <c>accept-before-merge</c> that has no accepted Task to merge, or <see langword="null"/>.</summary>
    /// <param name="personaName">The Persona running the command.</param>
    /// <param name="acceptTeams">The Persona's Teams that have the gate on.</param>
    /// <param name="command">The shell line; a line that only merges locally into main is passed by a released Task, a line that pushes never is.</param>
    private string? AcceptRequiredRefusal(string personaName, HashSet<string> acceptTeams, string command)
    {
        if (acceptTeams.Count == 0)
        {
            return null;
        }

        bool localOnly = GitCommandLine.OnlyLocalMergesIntoMain(command);
        bool released = false;
        bool runsInside = false;
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

            // The level is read again, so a scope set back to Ask me (or past its spend cap) stops passing merges at once.
            if (localOnly && NeedsYouRelease.Holds(task, now) && levels?.ForTask(task).Level == NeedsYouLevel.RunInsideTheTeam)
            {
                return null;
            }

            released |= NeedsYouRelease.LatestAt(task) is not null;
            runsInside |= !task.Status.IsTerminal() && levels?.ForTask(task).Level == NeedsYouLevel.RunInsideTheTeam;
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

        return NoAcceptRefusal(own, GitCommandLine.HasPush(command), released, runsInside);
    }

    /// <summary>The refusal text when no Task of the Team carries the command: a push, a merge under Run inside the Team, or a merge under Ask me.</summary>
    /// <param name="own">The Persona's open Tasks of the Team.</param>
    /// <param name="push">Whether the line pushes.</param>
    /// <param name="released">Whether a Task of the Team has been released.</param>
    /// <param name="runsInside">Whether an open Task of the Team is under the level Run inside the Team.</param>
    private static string NoAcceptRefusal(List<TaskItem> own, bool push, bool released, bool runsInside)
    {
        if (push && (released || runsInside))
        {
            string pushWhere = own.Count == 1
                ? string.Create(CultureInfo.InvariantCulture, $"Task {own[0].Id}'s Action")
                : "the Action of the Task this push belongs to";
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Cannot push: a push leaves the Team's repository and waits for the Human's Accept at every Needs You level. Put the push in {pushWhere} with update_task, for example 'Push main to origin'. Huddle moves the Task to Needs You when its Proof is complete; push after the Human accepts it. Do not ask for the Accept in chat.");
        }

        if (!push && runsInside)
        {
            return "Cannot merge yet: under Run inside the Team a merge into main runs once a Task's Proof is complete in Review, and no Task of your Team is ready. Finish review and QA Evidence first; do not set a merge Action.";
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
            if ((!task.Status.IsTerminal() || InDoneWindow(task, now) || NeedsYouRelease.InDoneWindow(task, now))
                && string.Equals(task.Location.Team, team, StringComparison.OrdinalIgnoreCase))
            {
                evidence.AddRange(task.Evidence);
            }
        }

        return evidence;
    }
}
