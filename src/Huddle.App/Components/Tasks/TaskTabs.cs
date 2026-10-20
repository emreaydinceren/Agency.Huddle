using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// How the app opens a Task for editing: always in the edit page's own Tab (<c>TaskEditPage</c>), whether the Task exists, is new, or is a copy.
/// A Tab is permanent, so a draft in progress is never replaced by the next page opened, and one Tab per address, so the same request goes
/// back to the Tab already open.
/// </summary>
internal static class TaskTabs
{
    /// <summary>Opens a blank new Task in its edit page, starting in <paramref name="team"/> and <paramref name="project"/> when they are known.</summary>
    /// <param name="tabs">The circuit's Tabs.</param>
    /// <param name="navigation">The navigation manager the router follows.</param>
    /// <param name="team">The Team to start in, or <see langword="null"/>.</param>
    /// <param name="project">The Project to start in, or <see langword="null"/>.</param>
    public static void OpenNew(TabService tabs, Microsoft.AspNetCore.Components.NavigationManager navigation, string? team, string? project)
    {
        List<string> query = [];
        if (!string.IsNullOrWhiteSpace(team))
        {
            query.Add("team=" + Uri.EscapeDataString(team));
        }

        if (!string.IsNullOrWhiteSpace(project))
        {
            query.Add("project=" + Uri.EscapeDataString(project));
        }

        Open(tabs, navigation, "/tasks/item/new/edit" + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty));
    }

    /// <summary>Opens a new Task that starts as a copy of <paramref name="id"/> in its edit page.</summary>
    /// <param name="tabs">The circuit's Tabs.</param>
    /// <param name="navigation">The navigation manager the router follows.</param>
    /// <param name="id">The Task to copy.</param>
    public static void OpenCopy(TabService tabs, Microsoft.AspNetCore.Components.NavigationManager navigation, TaskId id) =>
        Open(tabs, navigation, $"/tasks/item/{Uri.EscapeDataString(id.ToString())}/copy");

    /// <summary>Opens the Task <paramref name="id"/> in its edit page.</summary>
    /// <param name="tabs">The circuit's Tabs.</param>
    /// <param name="navigation">The navigation manager the router follows.</param>
    /// <param name="id">The Task to edit.</param>
    public static void OpenEdit(TabService tabs, Microsoft.AspNetCore.Components.NavigationManager navigation, TaskId id) =>
        Open(tabs, navigation, $"/tasks/item/{Uri.EscapeDataString(id.ToString())}/edit");

    /// <summary>The draft a copy of <paramref name="task"/> starts from: "Copy of" its title, in Backlog, with the rest of its fields; never its id, Change log or duplicate link.</summary>
    /// <param name="task">The Task to copy.</param>
    public static TaskDraft CopyOf(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new TaskDraft(
            $"Copy of {task.Title}",
            task.Location.Team,
            task.Location.Project,
            TaskState.Backlog,
            task.Priority,
            task.Assignee,
            task.OriginRoomId,
            task.Parent,
            task.BlockedBy,
            task.Tags,
            task.StartDate,
            task.DueDate,
            task.Description,
            task.Sprint);
    }

    private static void Open(TabService tabs, Microsoft.AspNetCore.Components.NavigationManager navigation, string url)
    {
        Tab target = tabs.Open(url);
        tabs.Keep(target.Id);
        navigation.NavigateTo(target.Url);
    }
}
