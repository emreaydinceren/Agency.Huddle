using Agency.Huddle.App.Tasks;
using MudBlazor;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>The Spec §13.10 mapping from a Task's state, priority or presence to a MudBlazor <see cref="Color"/>.</summary>
internal static class TaskColors
{
    /// <summary>Backlog Default, To Do Info, In Progress Warning, Review Secondary, Done Success, Cancelled/Duplicate/Rejected Error.</summary>
    public static Color For(TaskState state) => state switch
    {
        TaskState.Backlog => Color.Default,
        TaskState.ToDo => Color.Info,
        TaskState.InProgress => Color.Warning,
        TaskState.Review => Color.Secondary,
        TaskState.Done => Color.Success,
        TaskState.Cancelled or TaskState.Duplicate or TaskState.Rejected => Color.Error,
        _ => Color.Default,
    };

    /// <summary>Low Default, Medium Info, High Warning, Urgent Error.</summary>
    public static Color For(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => Color.Default,
        TaskPriority.Medium => Color.Info,
        TaskPriority.High => Color.Warning,
        TaskPriority.Urgent => Color.Error,
        _ => Color.Default,
    };

    /// <summary>Awake Success, Asleep Default, Offline Error.</summary>
    public static Color For(PresenceState presence) => presence switch
    {
        PresenceState.Awake => Color.Success,
        PresenceState.Asleep => Color.Default,
        PresenceState.Offline => Color.Error,
        _ => Color.Default,
    };

    /// <summary>The icon that always accompanies a priority alongside its colour and word.</summary>
    public static string Icon(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => Icons.Material.Filled.ArrowDownward,
        TaskPriority.Medium => Icons.Material.Filled.Remove,
        TaskPriority.High => Icons.Material.Filled.ArrowUpward,
        TaskPriority.Urgent => Icons.Material.Filled.PriorityHigh,
        _ => Icons.Material.Filled.Remove,
    };
}
