using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.Ordering;

/// <summary>
/// The <see cref="ITaskGroupRule"/> the panel and <c>next_task</c> use: it reads the Team's and the Project's
/// <c>oneInProgressPerProject</c> values through <see cref="ITeamFolders"/>, resolves them with <see cref="OneInProgressRule"/>,
/// and finds the group's In Progress Task in the <see cref="TaskStore"/>.
/// </summary>
/// <param name="folders">Reads <c>_team.md</c> and <c>_project.md</c>.</param>
/// <param name="store">Holds every active Task.</param>
internal sealed class TaskGroupRule(ITeamFolders folders, TaskStore store) : ITaskGroupRule
{
    /// <inheritdoc />
    public bool IsOn(string team, string? project)
    {
        RuleSetting teamSetting = folders.ReadTeamFile(team)?.OneInProgressPerProject ?? RuleSetting.Absent;
        RuleSetting projectSetting = project is null
            ? RuleSetting.Absent
            : folders.ReadProjectFile(team, project)?.OneInProgressPerProject ?? RuleSetting.Absent;
        return OneInProgressRule.Resolve(teamSetting, projectSetting, project is not null).IsOn;
    }

    /// <inheritdoc />
    public TaskItem? InProgressTask(string team, string? project) => store.All
        .Where(task => !task.Location.Closed
            && task.Status == TaskState.InProgress
            && string.Equals(task.Location.Team, team, StringComparison.OrdinalIgnoreCase)
            && string.Equals(task.Location.Project, project, StringComparison.OrdinalIgnoreCase))
        .OrderBy(static task => task.Id.ToString(), StringComparer.Ordinal)
        .FirstOrDefault();
}
