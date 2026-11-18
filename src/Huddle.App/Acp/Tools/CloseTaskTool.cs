namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Closes a Task, moving it into its Team's (or Project's) "_closed" folder (Spec §11.6): refused
/// when the Task is already Closed, and mirrors <see cref="ReopenTaskTool"/>. The body is
/// <see cref="TaskTransition"/>, shared with that tool.
/// </summary>
internal sealed class CloseTaskTool(
    TaskService tasks,
    TaskStore store,
    TaskTriggerService triggers,
    ITeamDirectory directory,
    IPromptSource prompts,
    string callerAgentId,
    GoldfishGuard? goldfish = null) : IAppTool
{
    private readonly TaskTransition transition = new(store, triggers, directory, callerAgentId, "Closed", target => tasks.Close(target.Task.Id, target.Actor), goldfish);

    /// <inheritdoc />
    public string Name => "close_task";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.closeTask.description");

    /// <inheritdoc />
    public JsonObject InputSchema => TaskToolText.TaskIdSchema();

    /// <inheritdoc />
    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return this.transition.InvokeAsync(arguments, this.Name, cancellationToken);
    }
}
