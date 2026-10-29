using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Wakes the Personas a Request lifecycle event names (RQ-4, RQ-7): maps each <see cref="WakeTarget"/> to a
/// Persona Name and delivers the wake through <see cref="TaskTriggerService"/>, so it shares that service's
/// per-Task wake budget and adds no counter of its own.
/// </summary>
/// <param name="trigger">Delivers one wake.</param>
/// <param name="teams">Names a Team's manager.</param>
/// <param name="personas">Finds the Chief of Staff by its built-in marker.</param>
/// <param name="logger">Records a target that resolved to nobody.</param>
internal sealed partial class RequestNotifier(TaskTriggerService trigger, ITeamCatalog teams, PersonaStore personas, ILogger<RequestNotifier> logger) : IRequestNotifier
{
    /// <inheritdoc />
    public async Task NotifyAsync(TaskItem request, RequestEvent requestEvent, IReadOnlyList<WakeTarget> targets, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        TaskActor wakeActor = new(TaskActorKind.Agent, actor, null);
#pragma warning disable HAA0102 // Enum.ToString returns the runtime's cached name, so a lookup table would save nothing.
        string summary = string.Concat("Request event: ", requestEvent.ToString());
#pragma warning restore HAA0102
        TaskId requestId = request.Id;
        HashSet<string> woken = new(StringComparer.OrdinalIgnoreCase);
        foreach (WakeTarget target in targets)
        {
            string? name = this.Resolve(request, target);
            if (string.IsNullOrEmpty(name))
            {
                LogNobodyToWake(logger, requestId, target);
                continue;
            }

            if (woken.Add(name))
            {
                _ = await trigger.WakeForRequestAsync(request, name, wakeActor, summary, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Logs that a wake target resolved to no Persona.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Request.</param>
    /// <param name="target">The target that resolved to nobody.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Request {TaskId}: nobody to wake for {Target}.")]
    private static partial void LogNobodyToWake(ILogger logger, TaskId taskId, WakeTarget target);

    /// <summary>Resolves a wake target to a Persona Name.</summary>
    /// <param name="request">The Request Task.</param>
    /// <param name="target">Who to wake.</param>
    /// <returns>The Persona Name, or <see langword="null"/> when the Team has no manager or there is no Chief of Staff.</returns>
    private string? Resolve(TaskItem request, WakeTarget target)
    {
        switch (target)
        {
            case WakeTarget.RequestingManager:
                Dictionary<string, string> frontmatter = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> field in request.UnknownFields)
                {
                    frontmatter[field.Key] = field.Value;
                }

                return RequestFields.TryRead(frontmatter, out RequestFields? fields) && fields is not null
                    ? teams.Find(fields.FromTeam)?.Manager
                    : null;
            case WakeTarget.ReceivingManager:
                return teams.Find(request.Location.Team)?.Manager;
            case WakeTarget.ChiefOfStaff:
                return personas.Entries
                    .FirstOrDefault(static entry => string.Equals(entry.Builtin, BuiltinAgent.ChiefOfStaffMarker, StringComparison.Ordinal))?.Name;
            default:
                return null;
        }
    }
}
