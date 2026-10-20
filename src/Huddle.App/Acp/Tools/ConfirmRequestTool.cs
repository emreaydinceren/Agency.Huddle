using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// The requesting Team's QA confirming a finished Request before it closes (RQ-5): the caller must be a member of the
/// <c>from_team</c> whose role covers <c>verify</c>, and the Request must be Done. Records <c>confirmed_by</c>, then closes the
/// Task through <see cref="TaskService.Close"/>. Offered only to a Persona holding a Skill that lists it.
/// </summary>
internal sealed class ConfirmRequestTool(
    TaskService tasks,
    TaskStore store,
    IRequestConfirmers confirmers,
    IRequestNotifier notifier,
    ITeamDirectory directory,
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    /// <inheritdoc />
    public string Name => "confirm_request";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.confirmRequest.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["taskId"] = new JsonObject { ["type"] = "string" },
            ["note"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "taskId" },
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

        if (!TaskToolText.TryGetString(arguments, "taskId", out string? taskIdText, out string taskIdRefusal))
        {
            return taskIdRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "note", out string? noteText, out string noteRefusal))
        {
            return noteRefusal;
        }

        if (!RequestTaskLookup.TryResolve(store, taskIdText, out TaskItem? request, out RequestFields? fields, out string lookupRefusal))
        {
            return lookupRefusal;
        }

        bool isRequesterQa = confirmers.IsVerifierOf(fields.FromTeam, actor.Name);
        RequestTransition transition = RequestLifecycle.Apply(new RequestState(request.Status.ToWire(), fields.Triage), RequestEvent.Confirm, isRequesterQa);
        if (!transition.Allowed)
        {
            return transition.Error ?? string.Empty;
        }

        TaskPatch patch = BuildConfirmationPatch(request, fields, actor, noteText);

        if (!TaskToolText.TryRun(() => tasks.Update(request.Id, patch, request.Version, actor), out TaskResult? updateResult, out string updateRunRefusal))
        {
            return updateRunRefusal;
        }

        if (!RequestTaskLookup.TrySaved(updateResult,out TaskItem? confirmed, out string updateRefusal))
        {
            return updateRefusal;
        }

        if (!TaskToolText.TryRun(() => tasks.Close(confirmed.Id, actor), out TaskResult? closeResult, out string closeRunRefusal))
        {
            this.RollBack(request, fields, actor);
            return closeRunRefusal;
        }

        if (!RequestTaskLookup.TrySaved(closeResult,out TaskItem? closed, out string closeRefusal))
        {
            this.RollBack(request, fields, actor);
            return closeRefusal;
        }

        await notifier.NotifyAsync(closed, RequestEvent.Confirm, transition.Wake, actor.Name, cancellationToken).ConfigureAwait(false);

        return string.Create(CultureInfo.InvariantCulture, $"Confirmed {closed.Id}; it is closed.");
    }

    /// <summary>Builds the patch that records <c>confirmed_by</c> and appends the optional note under a Confirmation heading.</summary>
    /// <param name="request">The Request being confirmed.</param>
    /// <param name="fields">The Request fields as they are.</param>
    /// <param name="actor">The confirming caller.</param>
    /// <param name="noteText">The raw <c>note</c> argument, if any.</param>
    private static TaskPatch BuildConfirmationPatch(TaskItem request, RequestFields fields, TaskActor actor, string? noteText)
    {
        string note = (noteText ?? string.Empty).Trim();
        return new TaskPatch
        {
            Description = note.Length > 0 ? string.Concat(request.Description, "\n\n## Confirmation\n", note) : null,
            ExtraFields = [.. (fields with { ConfirmedBy = actor.Name }).ToFrontmatter()],
        };
    }

    /// <summary>Puts back the Request fields and body that confirming changed, after closing was refused.</summary>
    /// <param name="original">The Request as it was before the confirmation was written.</param>
    /// <param name="fields">The Request fields as they were.</param>
    /// <param name="actor">The caller.</param>
    private void RollBack(TaskItem original, RequestFields fields, TaskActor actor)
    {
        TaskPatch patch = new() { Description = original.Description, ExtraFields = [.. fields.ToFrontmatter()] };
        _ = TaskToolText.TryRun(() => tasks.Update(original.Id, patch, null, actor), out _, out _);
    }
}
