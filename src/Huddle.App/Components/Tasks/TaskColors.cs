using Agency.Huddle.App.Tasks;
using MudBlazor;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>The Spec §13.10 mapping from a Task's state, priority or presence to a MudBlazor <see cref="Color"/>.</summary>
internal static class TaskColors
{
    /// <summary>Backlog and To Do Default, In Progress Info, Needs You Tertiary (its own signal, so it never reads as the Accent or as In Progress), Done Success, Cancelled/Duplicate/Rejected Error. Amber (Warning) means "needs attention" only, so no state uses it.</summary>
    public static Color For(TaskState state) => state switch
    {
        TaskState.Backlog => Color.Default,
        TaskState.ToDo => Color.Default,
        TaskState.InProgress => Color.Info,
        TaskState.NeedsYou => Color.Tertiary,
        TaskState.Done => Color.Success,
        TaskState.Cancelled or TaskState.Duplicate or TaskState.Rejected => Color.Error,
        _ => Color.Default,
    };

    /// <summary>Low and Medium Default (both mean "no special attention", so they differ by glyph only), High Warning, Urgent Error.</summary>
    public static Color For(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => Color.Default,
        TaskPriority.Medium => Color.Default,
        TaskPriority.High => Color.Warning,
        TaskPriority.Urgent => Color.Error,
        _ => Color.Default,
    };

    /// <summary>Awake Info (a Turn running: thinking; amber is only "needs attention"), Asleep and Offline Default (a hollow grey ring); red is kept for Degraded Health.</summary>
    public static Color For(PresenceState presence) => presence switch
    {
        PresenceState.Awake => Color.Info,
        PresenceState.Asleep => Color.Default,
        PresenceState.Offline => Color.Default,
        _ => Color.Default,
    };

    /// <summary>
    /// The glyph of a state: the same ring the List's Status chip and the Board's column header draw (<c>.task-status-chip::before</c>,
    /// <c>.task-state-dot</c>), as SVG markup for a 24-unit icon box: Backlog a dashed ring, To Do a ring, In Progress a half-filled ring,
    /// Needs You a solid dot, Done and the three closed states a solid dot in their colour.
    /// </summary>
    public static string Icon(TaskState state) => state switch
    {
        TaskState.Backlog => Ring("var(--mud-palette-text-disabled)", dashed: true),
        TaskState.ToDo => Ring("var(--hu-text-subtle)"),
        TaskState.InProgress => Ring("var(--mud-palette-info)") + """<path d="M12 5.75A6.25 6.25 0 0 0 12 18.25Z" fill="var(--mud-palette-info)"/>""",
        TaskState.NeedsYou => Ring("var(--mud-palette-tertiary)", fill: "var(--mud-palette-tertiary)"),
        TaskState.Done => Ring("var(--mud-palette-success)", fill: "var(--mud-palette-success)"),
        TaskState.Cancelled or TaskState.Duplicate or TaskState.Rejected => Ring("var(--mud-palette-error)", fill: "var(--mud-palette-error)"),
        _ => Ring("var(--hu-text-subtle)"),
    };

    /// <summary>The icon that always accompanies a priority alongside its colour and word.</summary>
    public static string Icon(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => Icons.Material.Filled.KeyboardArrowDown,
        TaskPriority.Medium => Icons.Material.Filled.Remove,
        TaskPriority.High => Icons.Material.Filled.KeyboardArrowUp,
        TaskPriority.Urgent => Icons.Material.Filled.KeyboardDoubleArrowUp,
        _ => Icons.Material.Filled.Remove,
    };

    /// <summary>One 12.5-unit ring, hollow or filled, solid or dashed.</summary>
    /// <param name="stroke">The ring's colour.</param>
    /// <param name="fill">The fill colour, or <see langword="null"/> for a hollow ring.</param>
    /// <param name="dashed">Whether the ring is dashed.</param>
    private static string Ring(string stroke, string? fill = null, bool dashed = false) =>
        $"""<circle cx="12" cy="12" r="6.25" fill="{fill ?? "none"}" stroke="{stroke}" stroke-width="2"{(dashed ? " stroke-dasharray=\"3 2.4\"" : string.Empty)}/>""";
}
