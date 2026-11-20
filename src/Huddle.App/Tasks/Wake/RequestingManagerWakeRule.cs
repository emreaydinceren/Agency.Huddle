using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>
/// RQ-4: wakes the manager of the Team that filed a Request on every status change of that Request,
/// unless that manager made the change, is the assignee or the receiving Team's manager (both woken
/// above), is the Human, or is no known Persona. A Request whose requesting Team has no manager wakes
/// nobody and is logged. The wake goes through the service's delivery, so it shares the per-Task wake budget.
/// </summary>
/// <param name="teams">Names a Team's manager; <see langword="null"/> wakes nobody.</param>
/// <param name="human">The Human's live identity, who is never woken.</param>
/// <param name="personas">Resolves whether the manager is a known Persona.</param>
/// <param name="options">Read live for <c>Tasks.Enabled</c> and <c>Tasks.WakeEnabled</c>.</param>
/// <param name="logger">Records that a requesting Team has no manager; <see langword="null"/> records nothing.</param>
internal sealed partial class RequestingManagerWakeRule(ITeamCatalog? teams, IHumanIdentity human, PersonaStore personas, TeamOptions options, ILogger? logger = null) : ITaskWakeRule
{
    /// <inheritdoc />
    public string Name => "RequestingManager";

    /// <inheritdoc />
    public TaskWakeChoice Choose(TaskWakeRound round)
    {
        ArgumentNullException.ThrowIfNull(round);

        TaskItem latest = round.Latest;
        if (!round.RequestStatusWakes || !options.Tasks.Enabled || !options.Tasks.WakeEnabled)
        {
            return new TaskWakeChoice.Skip(null);
        }

        Dictionary<string, string> frontmatter = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> field in latest.UnknownFields)
        {
            frontmatter[field.Key] = field.Value;
        }

        if (!RequestFields.TryRead(frontmatter, out RequestFields? request) || request is null)
        {
            return new TaskWakeChoice.Skip(null);
        }

        string? manager = teams?.Find(request.FromTeam)?.Manager;
        if (string.IsNullOrEmpty(manager))
        {
            if (logger is not null)
            {
                LogNoRequestingManager(logger, latest.Id, request.FromTeam);
            }

            return new TaskWakeChoice.Skip(null);
        }

        if (string.Equals(manager, GoldfishStamp.AgentName(round.Actor.Name),StringComparison.OrdinalIgnoreCase)
            || string.Equals(manager, latest.Assignee, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manager, teams?.Find(latest.Location.Team)?.Manager, StringComparison.OrdinalIgnoreCase)
            || human.Matches(manager)
            || personas.Get(manager) is null)
        {
            return new TaskWakeChoice.Skip(null);
        }

        return new TaskWakeChoice.Wake(manager);
    }

    /// <summary>Logs that a Request's requesting Team has no manager to wake.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Request.</param>
    /// <param name="team">The requesting Team.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Not waking the requesting manager of Request {TaskId}: team '{Team}' has no manager.")]
    private static partial void LogNoRequestingManager(ILogger logger, TaskId taskId, string team);
}
