using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>
/// #403: a Task that entered Review in this batch must reach someone who can review it. When the assignee can't (the builder
/// still holds it), the Team's first member who can review is woken once, through the service's delivery so the same wake budget
/// applies. Nobody is woken when the assignee can review (they were woken above), when the one who can made the change, or is the
/// manager (woken above); a Team with nobody who can review is left as before.
/// </summary>
/// <param name="teams">Names a Team's manager and members.</param>
/// <param name="human">The Human's live identity, who is never woken.</param>
/// <param name="personas">Resolves whether a member is a known Persona.</param>
/// <param name="confirmers">Says who on a Team can review; <see langword="null"/> wakes nobody.</param>
/// <param name="options">Read live for <c>Tasks.Enabled</c> and <c>Tasks.WakeEnabled</c>.</param>
internal sealed class ChallengerWakeRule(ITeamCatalog? teams, IHumanIdentity human, PersonaStore personas, IRequestConfirmers? confirmers, TeamOptions options) : ITaskWakeRule
{
    /// <inheritdoc />
    public string Name => "Challenger";

    /// <inheritdoc />
    public TaskWakeChoice Choose(TaskWakeRound round)
    {
        ArgumentNullException.ThrowIfNull(round);

        TaskItem latest = round.Latest;
        if (!round.MovedToReview
            || latest.Status != TaskState.Review
            || confirmers is not { } challengers
            || !options.Tasks.Enabled
            || !options.Tasks.WakeEnabled)
        {
            return new TaskWakeChoice.Skip(null);
        }

        string team = latest.Location.Team;
        string? manager = teams?.Find(team)?.Manager;
        if (latest.Assignee is { Length: > 0 } assignee && challengers.IsChallengerOf(team, assignee))
        {
            return new TaskWakeChoice.Skip(null);
        }

        if (manager is { Length: > 0 }
            && !string.Equals(manager, GoldfishStamp.AgentName(round.Actor.Name),StringComparison.OrdinalIgnoreCase)
            && !string.Equals(manager, latest.Assignee, StringComparison.OrdinalIgnoreCase))
        {
            // The manager was woken for this entry and hands it to whoever reviews; only an entry that reached nobody capable needs this wake.
            return new TaskWakeChoice.Skip(null);
        }

        IReadOnlyList<string> members = teams?.Find(team)?.Members ?? [];
        string? challenger = members.FirstOrDefault(member =>
            challengers.IsChallengerOf(team, member)
            && !string.Equals(member, GoldfishStamp.AgentName(round.Actor.Name),StringComparison.OrdinalIgnoreCase)
            && !string.Equals(member, latest.Assignee, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(member, manager, StringComparison.OrdinalIgnoreCase)
            && !human.Matches(member)
            && personas.Get(member) is not null);
        return challenger is null
            ? new TaskWakeChoice.Skip(null)
            : new TaskWakeChoice.Wake(challenger, AsChallenger: true);
    }
}
