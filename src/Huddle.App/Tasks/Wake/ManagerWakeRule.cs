using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>
/// OR-2: wakes the Team's manager (from <c>_team.md</c>) about the same change, unless wake-ups
/// are off, the manager made the change, already is the assignee (so was woken above), is the
/// Human, or is no known Persona. The wake goes through the service's delivery, so it consumes
/// the same per-Task Agent-made wake budget as the assignee's. A Team with no manager, or no
/// catalog, behaves as before.
/// </summary>
/// <param name="teams">Names a Team's manager; <see langword="null"/> wakes nobody.</param>
/// <param name="human">The Human's live identity, who is never woken.</param>
/// <param name="personas">Resolves whether the manager is a known Persona.</param>
/// <param name="options">Read live for <c>Tasks.Enabled</c> and <c>Tasks.WakeEnabled</c>.</param>
internal sealed class ManagerWakeRule(ITeamCatalog? teams, IHumanIdentity human, PersonaStore personas, TeamOptions options) : ITaskWakeRule
{
    /// <inheritdoc />
    public string Name => "Manager";

    /// <inheritdoc />
    public TaskWakeChoice Choose(TaskWakeRound round)
    {
        ArgumentNullException.ThrowIfNull(round);

        TaskItem latest = round.Latest;
        if (teams?.Find(latest.Location.Team)?.Manager is not { Length: > 0 } manager
            || !options.Tasks.Enabled
            || !options.Tasks.WakeEnabled)
        {
            return new TaskWakeChoice.Skip(null);
        }

        if (string.Equals(manager, GoldfishStamp.AgentName(round.Actor.Name),StringComparison.OrdinalIgnoreCase)
            || string.Equals(manager, latest.Assignee, StringComparison.OrdinalIgnoreCase)
            || human.Matches(manager)
            || personas.Get(manager) is null)
        {
            return new TaskWakeChoice.Skip(null);
        }

        return new TaskWakeChoice.Wake(manager);
    }
}
