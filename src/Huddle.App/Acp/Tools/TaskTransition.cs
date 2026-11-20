using System.Text.Json.Nodes;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// The body <see cref="CloseTaskTool"/> and <see cref="ReopenTaskTool"/> share (Spec §11.6): resolve
/// the Task and the caller, run the one <see cref="TaskService"/> move, and answer
/// "{Verb} {id}." followed by the notify clause, or the refusal. The two tools differ only in the
/// move and the verb, so each holds one of these rather than repeating the body.
/// </summary>
/// <param name="store">The Task store the <c>taskId</c> argument is looked up in.</param>
/// <param name="triggers">Previews who the move will notify.</param>
/// <param name="directory">Resolves the calling Agent.</param>
/// <param name="callerAgentId">The calling Agent's user id.</param>
/// <param name="verb">The past-tense verb of the success text, such as <c>Closed</c>.</param>
/// <param name="move">The <see cref="TaskService"/> call that moves the Task. It never returns <see cref="TaskResult.Unchanged"/>: <c>CloseOrReopenCore</c> refuses an already-moved Task instead of diffing it.</param>
/// <param name="goldfish">Stamps the caller's Actor while the caller is possessed.</param>
internal sealed class TaskTransition(
    TaskStore store,
    TaskTriggerService triggers,
    ITeamDirectory directory,
    string callerAgentId,
    string verb,
    Func<TaskToolTarget, TaskResult> move,
    GoldfishGuard? goldfish = null)
{
    /// <summary>Runs the move for the Task <paramref name="arguments"/> names and words the outcome; never throws for a refusal.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="toolName">The calling tool's name, for the exception a result it cannot produce would raise.</param>
    /// <param name="cancellationToken">Propagated to the caller lookup.</param>
    public async Task<string> InvokeAsync(JsonObject arguments, string toolName, CancellationToken cancellationToken)
    {
        (TaskToolTarget? target, string refusal) = await TaskToolText.StartAsync(arguments, store, directory, callerAgentId, cancellationToken, goldfish).ConfigureAwait(false);
        if (target is null)
        {
            return refusal;
        }

        if (!TaskToolText.TryRun(() => move(target), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        if (result is not TaskResult.Saved saved)
        {
            return TaskToolText.Refusal(result, toolName);
        }

        WakePreview preview = triggers.Preview(saved.Change, target.Actor);
        string notify = TaskToolText.NotifyClause(preview, target.Actor.Name);
        return $"{verb} {saved.Task.Id}. {notify}";
    }
}
