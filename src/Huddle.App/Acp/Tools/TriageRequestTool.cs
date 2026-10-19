using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
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
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "triage_request";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.triageRequest.description", NoValues);

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

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return actorRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "taskId", out string? taskIdText, out string argumentRefusal)
            || !TaskToolText.TryGetString(arguments, "decision", out string? decisionText, out argumentRefusal)
            || !TaskToolText.TryGetString(arguments, "reason", out string? reasonText, out argumentRefusal)
            || !TaskToolText.TryGetString(arguments, "assignee", out string? assigneeText, out argumentRefusal))
        {
            return argumentRefusal;
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

        string reason = (reasonText ?? string.Empty).Trim();
        if (!TryParseDecision(decisionText, reason, out RequestEvent requestEvent, out string decisionRefusal))
        {
            return decisionRefusal;
        }

        RequestTransition transition = RequestLifecycle.Apply(new RequestState(request.Status.ToWire(), fields.Triage), requestEvent);
        if (!transition.Allowed || transition.Next is null)
        {
            return transition.Error ?? string.Empty;
        }

        string assignee = (assigneeText ?? string.Empty).Trim();
        if (requestEvent == RequestEvent.Accept && assignee.Length > 0 && !receiving.Members.Contains(assignee, StringComparer.OrdinalIgnoreCase))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{assignee} is not a member of {receiving.Name}.");
        }

        TaskPatch patch = BuildPatch(request, fields, transition.Next, requestEvent, reason, assignee);

        if (!this.TryUpdate(request, patch, actor, out TaskItem? saved, out string saveRefusal))
        {
            return saveRefusal;
        }

        await notifier.NotifyAsync(saved, requestEvent, transition.Wake, actor.Name, cancellationToken).ConfigureAwait(false);

        return DescribeOutcome(requestEvent, saved, assignee);
    }

    /// <summary>Applies the patch through <see cref="TaskService.Update"/> and unwraps the saved Request.</summary>
    /// <param name="request">The Request being triaged.</param>
    /// <param name="patch">The patch to apply.</param>
    /// <param name="actor">The caller.</param>
    /// <param name="saved">The saved Request when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private bool TryUpdate(TaskItem request, TaskPatch patch, TaskActor actor, [NotNullWhen(true)] out TaskItem? saved, out string refusal)
    {
        saved = null;
        if (!TaskToolText.TryRun(() => tasks.Update(request.Id, patch, request.Version, actor), out TaskResult? result, out refusal))
        {
            return false;
        }

        return RequestTaskLookup.TrySaved(result, out saved, out refusal);
    }

    /// <summary>Reads the <c>decision</c> word, and checks a decline or needs_info comes with a reason.</summary>
    /// <param name="decisionText">The raw <c>decision</c> argument.</param>
    /// <param name="reason">The trimmed <c>reason</c> argument.</param>
    /// <param name="requestEvent">The lifecycle event the decision maps to.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryParseDecision(string? decisionText, string reason, out RequestEvent requestEvent, out string refusal)
    {
        refusal = string.Empty;
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
                requestEvent = default;
                refusal = "'decision' must be one of accept, decline, needs_info.";
                return false;
        }

        if (requestEvent == RequestEvent.Decline && reason.Length == 0)
        {
            refusal = "A decline needs a 'reason'.";
            return false;
        }

        if (requestEvent == RequestEvent.MarkNeedsInfo && reason.Length == 0)
        {
            refusal = "A needs_info triage needs a 'reason' saying what is missing.";
            return false;
        }

        return true;
    }

    /// <summary>Builds the patch that applies the triage: the new triage state, and the status, reason, assignee or note the event calls for.</summary>
    /// <param name="request">The Request being triaged.</param>
    /// <param name="fields">The Request fields as they are.</param>
    /// <param name="next">The state the lifecycle moves the Request to.</param>
    /// <param name="requestEvent">The triage event.</param>
    /// <param name="reason">The trimmed reason, possibly empty.</param>
    /// <param name="assignee">The trimmed assignee, possibly empty.</param>
    private static TaskPatch BuildPatch(TaskItem request, RequestFields fields, RequestState next, RequestEvent requestEvent, string reason, string assignee)
    {
        RequestFields nextFields = fields with
        {
            Triage = next.Triage,
            DeclineReason = requestEvent == RequestEvent.Decline ? reason : fields.DeclineReason,
        };
        return new TaskPatch
        {
            Status = TaskStates.TryParse(next.Status, out TaskState status) && status != request.Status ? status : null,
            Reason = requestEvent == RequestEvent.Decline ? reason : null,
            Assignee = requestEvent == RequestEvent.Accept && assignee.Length > 0 ? Optional<string?>.Set(assignee) : default,
            Description = requestEvent == RequestEvent.MarkNeedsInfo ? string.Concat(request.Description, "\n\n## Triage\n", reason) : null,
            ExtraFields = [.. nextFields.ToFrontmatter()],
        };
    }

    /// <summary>Words the success text for the triage event.</summary>
    /// <param name="requestEvent">The triage event.</param>
    /// <param name="saved">The saved Request.</param>
    /// <param name="assignee">The trimmed assignee, possibly empty.</param>
    private static string DescribeOutcome(RequestEvent requestEvent, TaskItem saved, string assignee)
    {
        return requestEvent switch
        {
            RequestEvent.Accept when assignee.Length > 0 => string.Create(CultureInfo.InvariantCulture, $"Accepted {saved.Id}; assigned to {assignee}."),
            RequestEvent.Accept => string.Create(CultureInfo.InvariantCulture, $"Accepted {saved.Id}."),
            RequestEvent.Decline => string.Create(CultureInfo.InvariantCulture, $"Declined {saved.Id}."),
            _ => string.Create(CultureInfo.InvariantCulture, $"Asked for more information on {saved.Id}."),
        };
    }
}
