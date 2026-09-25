namespace Agency.Huddle.App.Acp.Tools;

using System.Diagnostics;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Closes a Task, moving it into its Team's (or Project's) "_closed" folder (Spec §11.6): refused
/// when the Task is already Closed, and mirrors <see cref="ReopenTaskTool"/>.
/// </summary>
internal sealed class CloseTaskTool(
    TaskService tasks,
    TaskStore store,
    TaskTriggerService triggers,
    ITeamDirectory directory,
    IPromptSource prompts,
    string callerAgentId) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "close_task";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.closeTask.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["taskId"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "taskId" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TaskToolText.TryGetString(arguments, "taskId", out string? taskIdText, out string taskIdRefusal))
        {
            return taskIdRefusal;
        }

        if (!TaskToolText.TryResolve(store, taskIdText, out TaskItem? current, out string resolveRefusal))
        {
            return resolveRefusal;
        }

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken);
        if (actor is null)
        {
            return actorRefusal;
        }

        if (!TaskToolText.TryRun(() => tasks.Close(current.Id, actor), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        return result switch
        {
            TaskResult.Saved saved => this.FormatSaved(saved, actor),
            TaskResult.Refused refused => string.Join('\n', refused.Problems),
            TaskResult.NotFound notFound => $"Unknown task '{notFound.Id}'.",
            TaskResult.Conflict conflict => $"{conflict.Current.Id} was changed by someone else; please try again.",

            // CloseOrReopenCore never diffs against an Unchanged snapshot (it skips straight from the
            // already-closed check to writing), so Unchanged is the only case left uncovered above,
            // and TaskService.Close never produces it.
            _ => throw new UnreachableException("close_task's TaskService.Close never returns Unchanged; every other TaskResult case is handled above."),
        };
    }

    /// <summary>Formats the Spec §11.6 success text: "Closed {id}." followed by the notify clause from <see cref="TaskTriggerService.Preview"/>.</summary>
    private string FormatSaved(TaskResult.Saved saved, TaskActor actor)
    {
        WakePreview preview = triggers.Preview(saved.Change.Before, saved.Task, actor);
        string notify = TaskToolText.NotifyClause(preview, actor.Name);
        return $"Closed {saved.Task.Id}. {notify}";
    }
}
