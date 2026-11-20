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
    TaskTriggerService triggers,
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

        if (result is not TaskResult.Saved saved)
        {
            return TaskToolText.Refusal(result, this.Name);
        }

        // The tool only reports who the comment wakes; the trigger service delivers the wake when the batch fires.
        string savedText = saved.Task.Comments.Count > 0 ? saved.Task.Comments[^1].Text : text.Trim();
        IReadOnlyList<string> woken = await triggers.ResolveCommentMentionsAsync(saved.Task, savedText, target.Actor, cancellationToken).ConfigureAwait(false);
        return $"Added a comment to {saved.Task.Id}. {WakeSentence(triggers.WakesEnabled, woken)}";
    }

    /// <summary>The reply's last sentence: who the comment wakes, or why no one is woken and how to change that.</summary>
    /// <param name="wakesEnabled">Whether Tasks and wake-ups are on.</param>
    /// <param name="woken">The Agent names the comment Mentions, in first-mention order.</param>
    /// <returns>The sentence, ending in a full stop.</returns>
    private static string WakeSentence(bool wakesEnabled, IReadOnlyList<string> woken)
    {
        if (!wakesEnabled)
        {
            return "No one is woken: wake-ups are off.";
        }

        if (woken.Count == 0)
        {
            return "No one is woken: Mention an Agent with @Name to wake them.";
        }

        string first = string.Join(", ", woken.Take(woken.Count - 1));
        return woken.Count == 1 ? $"Wakes {woken[0]}." : $"Wakes {first} and {woken[^1]}.";
    }
}
