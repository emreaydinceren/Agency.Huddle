namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Reopens a Task, moving it back out of "_closed" (Spec §11.6): refused when the Task is already
/// Active, and mirrors <see cref="CloseTaskTool"/>. The body is <see cref="TaskTransition"/>, shared
/// with that tool.
/// </summary>
internal sealed class ReopenTaskTool(
    TaskService tasks,
    TaskStore store,
    TaskTriggerService triggers,
    ITeamDirectory directory,
    IPromptSource prompts,
    string callerAgentId,
    GoldfishGuard? goldfish = null) : IAppTool
{
    private readonly TaskTransition transition = new(store, triggers, directory, callerAgentId, "Reopened", target => tasks.Reopen(target.Task.Id, target.Actor), goldfish);

    /// <inheritdoc />
    public string Name => "reopen_task";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.reopenTask.description");

    /// <inheritdoc />
    public JsonObject InputSchema => TaskToolText.TaskIdSchema();

    /// <inheritdoc />
    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return this.transition.InvokeAsync(arguments, this.Name, cancellationToken);
    }
}
