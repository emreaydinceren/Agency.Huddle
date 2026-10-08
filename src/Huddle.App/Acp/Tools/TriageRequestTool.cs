using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// The receiving manager's triage of a Request (RQ-3): accept it into To Do, decline it with a reason, or ask the requester for
/// more information. Every change goes through <see cref="RequestLifecycle.Apply"/> and <see cref="TaskService.Update"/>, so
/// gates and the Change log still apply. Offered only to a Persona holding a Skill that lists it.
/// </summary>
internal sealed class TriageRequestTool(
    ITeamCatalog teams,
    TaskService tasks,
    TaskStore store,
    IRequestNotifier notifier,
    ITeamDirectory directory,
    string callerAgentId) : IAppTool
{
    /// <inheritdoc />
    public string Name => "triage_request";

    /// <inheritdoc />
    public string Description =>
        "Triages a Request filed with your Team. Decision 'accept' moves it to To Do (optionally name the assignee); 'decline' " +
        "rejects it and needs a reason; 'needs_info' keeps it in Backlog and needs a reason saying what is missing.";

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["taskId"] = new JsonObject { ["type"] = "string" },
            ["decision"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "accept", "decline", "needs_info" } },
            ["reason"] = new JsonObject { ["type"] = "string" },
            ["assignee"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "taskId", "decision" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken);
        if (actor is null)
        {
            return actorRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "taskId", out string? taskIdText, out string taskIdRefusal))
        {
            return taskIdRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "decision", out string? decisionText, out string decisionRefusal))
        {
            return decisionRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "reason", out string? reasonText, out string reasonRefusal))
        {
            return reasonRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "assignee", out string? assigneeText, out string assigneeRefusal))
        {
            return assigneeRefusal;
        }

        if (!RequestTaskLookup.TryResolve(store, taskIdText, out TaskItem? request, out RequestFields? fields, out string lookupRefusal))
        {
            return lookupRefusal;
        }

        TeamSummary? receiving = teams.Find(request.Location.Team);
        if (receiving is null || !string.Equals(receiving.Manager, actor.Name, StringComparison.OrdinalIgnoreCase))
        {
            return string.Create(CultureInfo.InvariantCulture, $"Only the manager of {request.Location.Team} can triage its Requests.");
        }

        RequestEvent requestEvent;
        switch ((decisionText ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "accept":
                requestEvent = RequestEvent.Accept;
                break;
            case "decline":
                requestEvent = RequestEvent.Decline;
                break;
            case "needs_info":
                requestEvent = RequestEvent.MarkNeedsInfo;
                break;
            default:
                return "'decision' must be one of accept, decline, needs_info.";
        }

        string reason = (reasonText ?? string.Empty).Trim();
        if (requestEvent == RequestEvent.Decline && reason.Length == 0)
        {
            return "A decline needs a 'reason'.";
        }

        if (requestEvent == RequestEvent.MarkNeedsInfo && reason.Length == 0)
        {
            return "A needs_info triage needs a 'reason' saying what is missing.";
        }

        RequestTransition transition = RequestLifecycle.Apply(new RequestState(request.Status.ToWire(), fields.Triage), requestEvent);
        if (!transition.Allowed || transition.Next is null)
        {
            return transition.Error ?? string.Empty;
        }

        string assignee = (assigneeText ?? string.Empty).Trim();
        RequestFields next = fields with
        {
            Triage = transition.Next.Triage,
            DeclineReason = requestEvent == RequestEvent.Decline ? reason : fields.DeclineReason,
        };
        TaskPatch patch = new()
        {
            Status = TaskStates.TryParse(transition.Next.Status, out TaskState status) && status != request.Status ? status : null,
            Reason = requestEvent == RequestEvent.Decline ? reason : null,
            Assignee = requestEvent == RequestEvent.Accept && assignee.Length > 0 ? Optional<string?>.Set(assignee) : default,
            Description = requestEvent == RequestEvent.MarkNeedsInfo ? string.Concat(request.Description, "\n\n## Triage\n", reason) : null,
            ExtraFields = [.. next.ToFrontmatter()],
        };

        if (!TaskToolText.TryRun(() => tasks.Update(request.Id, patch, request.Version, actor), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        if (!RequestTaskLookup.TrySaved(result,out TaskItem? saved, out string saveRefusal))
        {
            return saveRefusal;
        }

        await notifier.NotifyAsync(saved, requestEvent, transition.Wake, actor.Name, cancellationToken);

        return requestEvent switch
        {
            RequestEvent.Accept when assignee.Length > 0 => string.Create(CultureInfo.InvariantCulture, $"Accepted {saved.Id}; assigned to {assignee}."),
            RequestEvent.Accept => string.Create(CultureInfo.InvariantCulture, $"Accepted {saved.Id}."),
            RequestEvent.Decline => string.Create(CultureInfo.InvariantCulture, $"Declined {saved.Id}."),
            _ => string.Create(CultureInfo.InvariantCulture, $"Asked for more information on {saved.Id}."),
        };
    }
}
