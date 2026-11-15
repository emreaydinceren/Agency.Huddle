using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// The sentence <c>create_task</c> and <c>update_task</c> add to a success result when an Agent leaves a Task with an Action in a scope that
/// runs inside the Team: such a scope releases a local merge without an Action, and an Action sends the Task to the Human (ADR-0088). It
/// only informs; the call has already succeeded and nothing is refused.
/// </summary>
internal static class TaskActionNote
{
    /// <summary>Words the note for a Task, or nothing when no note applies.</summary>
    /// <param name="task">The Task as saved.</param>
    /// <param name="levels">Resolves the Task's level; <see langword="null"/> means Ask me everywhere, so there is no note.</param>
    /// <param name="gates">Says whether the Task's Team has <c>accept-before-merge</c>; <see langword="null"/> means it does not.</param>
    /// <param name="prompts">Resolves the note's text.</param>
    /// <returns>A space and the sentence, ready to append, or <see cref="string.Empty"/>.</returns>
    internal static string For(TaskItem task, NeedsYouLevels? levels, ProcessGateService? gates, IPromptSource prompts)
    {
        if (string.IsNullOrWhiteSpace(task.Action)
            || levels is null
            || gates is null
            || levels.ForTask(task).Level != NeedsYouLevel.RunInsideTheTeam
            || !gates.EnabledGates(task.Location.Team).Contains(GateId.AcceptBeforeMerge))
        {
            return string.Empty;
        }

        string scope = task.Location.Project is { Length: > 0 } project ? $"Project {project}" : $"Team {task.Location.Team}";
        return " " + prompts.Render(
            "tool.taskActionNote",
            new Dictionary<string, string> { ["{{taskId}}"] = task.Id.ToString(), ["{{scope}}"] = scope });
    }
}
