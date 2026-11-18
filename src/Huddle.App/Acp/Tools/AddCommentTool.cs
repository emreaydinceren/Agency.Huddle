namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Adds a comment to a Task's timeline (Spec §7.7, §11): free Markdown text under the caller's name and the clock, never edited
/// or deleted afterwards. The service trims it and refuses an empty or over-long one.
/// </summary>
internal sealed class AddCommentTool(
    TaskService tasks,
    TaskStore store,
    ITeamDirectory directory,
    string callerAgentId,
    IPromptSource prompts,
    GoldfishGuard? goldfish = null) : IAppTool
{
    /// <inheritdoc />
    public string Name => "add_comment";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.addComment.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["taskId"] = new JsonObject { ["type"] = "string" },
            ["text"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The comment, in Markdown. Several lines are fine.",
            },
        },
        ["required"] = new JsonArray { "taskId", "text" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        (TaskToolTarget? target, string refusal) = await TaskToolText.StartAsync(arguments, store, directory, callerAgentId, cancellationToken, goldfish).ConfigureAwait(false);
        if (target is null)
        {
            return refusal;
        }

        if (!TaskToolText.TryGetRequiredString(arguments, "text", out string? text, out string textRefusal))
        {
            return $"{textRefusal} Nothing was added.";
        }

        if (!TaskToolText.TryRun(() => tasks.AddComment(target.Task.Id, text, target.Actor), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        return result is TaskResult.Saved saved
            ? $"Added a comment to {saved.Task.Id}."
            : TaskToolText.Refusal(result, this.Name);
    }
}
